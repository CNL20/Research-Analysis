using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Application.Options;
using ScholarTrend.Application.Services;
using ScholarTrend.Domain.Constants;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Pdf;

namespace ScholarTrend.Infrastructure.Services;

/// <summary>
/// IPaperPdfProcessor với tính năng auto-parse sau khi download thành công.
///
/// Flow:
///   1. Download PDF (tương tự PaperPdfDownloadService gốc)
///   2. Auto-parse text (nếu config bật và chưa có text)
///
/// Lợi ích:
///   - Non-blocking: Approve API trả về ngay, parse chạy background
///   - Cache hit: Không parse lại nếu đã có ExtractedText
///   - Error isolation: Parse fail không ảnh hưởng download success
/// </summary>
public class AutoParsePdfProcessor : IPaperPdfProcessor
{
    private const int MaxDownloadAttempts = 3;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaperFileStorageProvider _storageProvider;
    private readonly IDocumentDownloader _downloader;
    private readonly IPaperPdfChannel _channel;
    private readonly PdfTextExtractionService _textExtractionService;
    private readonly PdfProcessingSettings _settings;
    private readonly ILogger<AutoParsePdfProcessor> _logger;

    public AutoParsePdfProcessor(
        IUnitOfWork unitOfWork,
        IPaperFileStorageProvider storageProvider,
        IDocumentDownloader downloader,
        IPaperPdfChannel channel,
        PdfTextExtractionService textExtractionService,
        IOptions<PdfProcessingSettings> settings,
        ILogger<AutoParsePdfProcessor> logger)
    {
        _unitOfWork = unitOfWork;
        _storageProvider = storageProvider;
        _downloader = downloader;
        _channel = channel;
        _textExtractionService = textExtractionService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task ProcessAsync(int paperPdfFileId, CancellationToken ct)
    {
        var record = await _unitOfWork.PaperPdfFiles.GetByIdAsync(paperPdfFileId);
        if (record is null)
        {
            _logger.LogWarning("PaperPdfFile #{Id} not found", paperPdfFileId);
            return;
        }

        // ===== PHASE 1: DOWNLOAD =====
        var downloadSuccess = await DownloadPdfAsync(record, ct);

        // ===== PHASE 2: AUTO-PARSE (chỉ nếu download thành công) =====
        if (downloadSuccess && _settings.AutoParseAfterDownload)
        {
            await AutoParseTextAsync(record, ct);
        }
    }

    private async Task<bool> DownloadPdfAsync(Domain.Entities.PaperPdfFile record, CancellationToken ct)
    {
        record.Status = PaperDownloadStatus.Downloading;
        record.AttemptCount++;
        await _unitOfWork.Context.SaveChangesAsync(ct);

        var sw = Stopwatch.StartNew();
        try
        {
            // 1. Validate URL safety (SSRF protection)
            if (!PdfUrlValidator.IsSafe(record.SourceUrl, out var urlError))
            {
                _logger.LogInformation(
                    "PDF download #{Id} skipped (URL validation): {Reason}",
                    record.Id, urlError);
                record.Status = PaperDownloadStatus.Skipped;
                record.FailureReason = urlError;
                record.CompletedAt = DateTime.UtcNow;
                await _unitOfWork.Context.SaveChangesAsync(ct);
                return false;
            }

            // 2. Download via IDocumentDownloader
            var doc = await _downloader.DownloadAsync(record.SourceUrl, ct);
            sw.Stop();

            if (doc is null)
            {
                throw new HttpRequestException("Download failed (null response from downloader)");
            }

            // 3. Validate đầy đủ (size + magic-bytes)
            var validationError = PdfValidationHelper.ValidateDownloadedPdf(
                record.SourceUrl, doc.Bytes, doc.ContentType);

            if (validationError != null)
            {
                throw new InvalidDataException(validationError);
            }

            // 4. Save to storage
            await _storageProvider.GetActiveStorage().SaveBytesAsync(record.LocalRelativePath, doc.Bytes, ct);
            var sha = ComputeSha256(doc.Bytes);

            // 5. Update record
            record.SizeBytes = doc.Bytes.Length;
            record.ContentType = doc.ContentType ?? "application/pdf";
            record.Sha256 = sha;
            record.Status = PaperDownloadStatus.Ready;
            record.CompletedAt = DateTime.UtcNow;
            record.FailureReason = null;
            await _unitOfWork.Context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "PDF download #{Id} completed in {Ms} ms ({Size:N0} bytes, sha256={Sha8})",
                record.Id, sw.ElapsedMilliseconds, doc.Bytes.Length, sha[..8]);

            return true;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex,
                "PDF download #{Id} failed on attempt {Attempt}/{Max}",
                record.Id, record.AttemptCount, MaxDownloadAttempts);

            if (record.AttemptCount < MaxDownloadAttempts)
            {
                record.Status = PaperDownloadStatus.Queued;
                await _unitOfWork.Context.SaveChangesAsync(ct);

                _ = Task.Run(async () =>
                {
                    var delayMs = (int)(Math.Pow(2, record.AttemptCount) * 1000);
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

            return false;
        }
    }

    private async Task AutoParseTextAsync(Domain.Entities.PaperPdfFile record, CancellationToken ct)
    {
        // Skip nếu đã có text (cache hit)
        if (!string.IsNullOrWhiteSpace(record.ExtractedText))
        {
            _logger.LogInformation(
                "PDF #{Id} already has extracted text ({Chars:N0} chars), skipping auto-parse",
                record.Id, record.ExtractedText.Length);
            return;
        }

        var parseSw = Stopwatch.StartNew();
        try
        {
            // Update status
            record.AnalysisStatus = PdfAnalysisStatus.Extracting;
            await _unitOfWork.Context.SaveChangesAsync(ct);

            // Parse với timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_settings.ParseTimeoutSeconds));

            var result = await _textExtractionService.ExtractForPaperAsync(
                record.ResearchPaperId,
                forceReExtract: false,
                timeoutCts.Token);

            parseSw.Stop();

            if (result.Status == "Extracted")
            {
                _logger.LogInformation(
                    "Auto-parsed PDF #{Id}: {Chars:N0} chars in {Ms} ms",
                    record.Id, result.CharacterCount, parseSw.ElapsedMilliseconds);

                record.AnalysisStatus = PdfAnalysisStatus.Completed;
                record.AnalysisError = null;
            }
            else
            {
                _logger.LogWarning(
                    "Auto-parse failed for PDF #{Id}: {Status} - {Error}",
                    record.Id, result.Status, result.ErrorMessage);

                record.AnalysisStatus = PdfAnalysisStatus.Failed;
                record.AnalysisError = $"{result.Status}: {result.ErrorMessage}";
            }
        }
        catch (OperationCanceledException)
        {
            parseSw.Stop();
            _logger.LogWarning(
                "Auto-parse timed out for PDF #{Id} after {Ms} ms",
                record.Id, parseSw.ElapsedMilliseconds);

            record.AnalysisStatus = PdfAnalysisStatus.Failed;
            record.AnalysisError = $"Parse timeout after {_settings.ParseTimeoutSeconds} seconds";
        }
        catch (Exception ex)
        {
            parseSw.Stop();
            _logger.LogError(ex,
                "Auto-parse error for PDF #{Id}",
                record.Id);

            record.AnalysisStatus = PdfAnalysisStatus.Failed;
            record.AnalysisError = $"{ex.GetType().Name}: {ex.Message}";
        }

        await _unitOfWork.Context.SaveChangesAsync(ct);
    }

    private static string ComputeSha256(byte[] bytes)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
