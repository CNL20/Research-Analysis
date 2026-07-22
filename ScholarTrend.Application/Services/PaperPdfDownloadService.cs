using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Constants;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Services;

/// <summary>
/// Triển khai IPaperPdfEnqueuer + IPaperPdfProcessor:
///   - EnqueueAsync: tạo record PaperPdfFile (Queued) + đẩy ID vào IPaperPdfChannel
///   - ProcessAsync: tải file về /uploads/papers/ và retry nếu fail
/// Channel được inject (Singleton) để cho phép HostedService (Singleton) đọc từ nó.
///
/// Validation (magic-bytes + size) dùng chung với PaperPdfDownloadOrchestrator qua
/// PdfValidationHelper - đảm bảo cả 2 path download đều reject file không phải PDF.
/// </summary>
public class PaperPdfDownloadService : IPaperPdfEnqueuer, IPaperPdfProcessor
{
    private const int MaxAttempts = 3;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaperFileStorageProvider _storageProvider;
    private readonly IDocumentDownloader _downloader;
    private readonly IPaperPdfChannel _channel;
    private readonly ILogger<PaperPdfDownloadService> _logger;

    public PaperPdfDownloadService(
        IUnitOfWork unitOfWork,
        IPaperFileStorageProvider storageProvider,
        IDocumentDownloader downloader,
        IPaperPdfChannel channel,
        ILogger<PaperPdfDownloadService> logger)
    {
        _unitOfWork = unitOfWork;
        _storageProvider = storageProvider;
        _downloader = downloader;
        _channel = channel;
        _logger = logger;
    }

    public async Task EnqueueAsync(string externalSource, string sourceUrl, int researchPaperId, CancellationToken ct = default)
    {
        var relativePath = $"papers/{researchPaperId}.pdf";
        var record = new PaperPdfFile
        {
            ResearchPaperId = researchPaperId,
            ExternalSource = externalSource,
            SourceUrl = sourceUrl,
            LocalRelativePath = relativePath,
            Status = PaperDownloadStatus.Queued,
            EnqueuedAt = DateTime.UtcNow,
            AttemptCount = 0
        };

        await _unitOfWork.PaperPdfFiles.AddAsync(record);
        await _unitOfWork.SaveChangesAsync();

        await _channel.Writer.WriteAsync(record.Id, ct);
        _logger.LogInformation(
            "Enqueued PDF download #{Id} for paper {PaperId} from {Source}",
            record.Id, researchPaperId, externalSource);
    }

    public async Task ProcessAsync(int paperPdfFileId, CancellationToken ct)
    {
        var record = await _unitOfWork.PaperPdfFiles.GetByIdAsync(paperPdfFileId);
        if (record is null)
        {
            _logger.LogWarning("PaperPdfFile #{Id} not found", paperPdfFileId);
            return;
        }

        record.Status = PaperDownloadStatus.Downloading;
        record.AttemptCount++;
        await _unitOfWork.Context.SaveChangesAsync(ct);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // 1. Validate URL safety (SSRF protection)
            if (!PdfUrlValidator.IsSafe(record.SourceUrl, out var urlError))
            {
                _logger.LogInformation(
                    "PDF download #{Id} skipped (URL validation): {Reason}",
                    paperPdfFileId, urlError);
                record.Status = PaperDownloadStatus.Skipped;
                record.FailureReason = urlError;
                record.CompletedAt = DateTime.UtcNow;
                await _unitOfWork.Context.SaveChangesAsync(ct);
                return;
            }

            // 2. Download via IDocumentDownloader
            var doc = await _downloader.DownloadAsync(record.SourceUrl, ct);
            sw.Stop();

            if (doc is null)
            {
                throw new HttpRequestException("Download failed (null response from downloader)");
            }

            // 3. Validate đầy đủ (size + magic-bytes) — dùng chung với orchestrator
            var validationError = PdfValidationHelper.ValidateDownloadedPdf(
                record.SourceUrl, doc.Bytes, doc.ContentType);

            if (validationError != null)
            {
                throw new InvalidDataException(validationError);
            }

            // 4. Save to disk (via storage provider → resolves B2 or Local per config)
            await _storageProvider.GetActiveStorage().SaveBytesAsync(record.LocalRelativePath, doc.Bytes, ct);
            var sha = ComputeSha256(doc.Bytes);

            // 5. Update record
            record.SizeBytes = doc.Bytes.Length;
            record.ContentType = doc.ContentType ?? "application/pdf";
            record.Sha256 = sha;
            record.Status = PaperDownloadStatus.Ready;
            record.CompletedAt = DateTime.UtcNow;
            await _unitOfWork.Context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "PDF download #{Id} completed in {Ms} ms ({Size:N0} bytes, sha256={Sha8})",
                paperPdfFileId, sw.ElapsedMilliseconds, doc.Bytes.Length, sha[..8]);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex,
                "PDF download #{Id} failed on attempt {Attempt}/{Max}",
                paperPdfFileId, record.AttemptCount, MaxAttempts);

            // Retry: push back to channel with exponential backoff
            if (record.AttemptCount < MaxAttempts)
            {
                record.Status = PaperDownloadStatus.Queued;
                await _unitOfWork.Context.SaveChangesAsync(ct);

                _ = Task.Run(async () =>
                {
                    var delayMs = (int)(Math.Pow(2, record.AttemptCount) * 1000);  // 2s, 4s, 8s
                    await Task.Delay(delayMs, ct);
                    await _channel.Writer.WriteAsync(record.Id, ct);
                }, ct);
            }
            else
            {
                record.Status = PaperDownloadStatus.Failed;
                record.FailureReason = $"{ex.GetType().Name}: {ex.Message}";
                record.CompletedAt = DateTime.UtcNow;
                await _unitOfWork.Context.SaveChangesAsync(ct);
            }
        }
    }

    private static string ComputeSha256(byte[] bytes)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

