using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Application.Services;
using ScholarTrend.Domain.Constants;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Services;

/// <summary>
/// Triển khai IPaperPdfDownloadOrchestrator — đảm bảo PDF tồn tại trên storage
/// (Local hoặc B2) cho một PaperPdfFile cụ thể.
///
/// Đây là single source of truth cho:
///   - URL safety check (PdfUrlValidator)
///   - Magic-bytes + size validation (PdfValidationHelper)
///   - Save to storage + update PaperPdfFile entity status
///
/// Được gọi bởi:
///   - PaperPdfDownloadOrchestrator.EnsurePdfForPaperAsync (on-demand khi user xin AI analyze)
///   - (Có thể mở rộng cho các use case khác sau này)
///
/// KHÔNG retry: caller (GeminiPdfAnalysisService) quyết định có retry hay không
/// và attach AnalysisStatus. Orchestrator chỉ tải 1 lần + báo success/fail.
/// </summary>
public class PaperPdfDownloadOrchestrator : IPaperPdfDownloadOrchestrator
{
    private readonly IPaperPdfFileRepository _pdfFileRepo;
    private readonly IResearchPaperRepository _paperRepo;
    private readonly IPaperFileStorageProvider _storageProvider;
    private readonly IDocumentDownloader _downloader;
    private readonly ILogger<PaperPdfDownloadOrchestrator> _logger;

    public PaperPdfDownloadOrchestrator(
        IPaperPdfFileRepository pdfFileRepo,
        IResearchPaperRepository paperRepo,
        IPaperFileStorageProvider storageProvider,
        IDocumentDownloader downloader,
        ILogger<PaperPdfDownloadOrchestrator> logger)
    {
        _pdfFileRepo = pdfFileRepo;
        _paperRepo = paperRepo;
        _storageProvider = storageProvider;
        _downloader = downloader;
        _logger = logger;
    }

    public async Task<PaperPdfFile?> EnsurePdfForPaperAsync(int researchPaperId, CancellationToken ct)
    {
        var paper = await _paperRepo.GetByIdAsync(researchPaperId);
        if (paper == null)
        {
            _logger.LogWarning("Paper {Id} not found for PDF download", researchPaperId);
            return null;
        }

        if (string.IsNullOrWhiteSpace(paper.PdfUrl))
        {
            _logger.LogWarning("Paper {Id} has no PdfUrl for download", researchPaperId);
            return null;
        }

        var pdfFile = await _pdfFileRepo.GetByResearchPaperIdAsync(researchPaperId);

        // Tạo entity nếu chưa có
        if (pdfFile == null)
        {
            pdfFile = new PaperPdfFile
            {
                ResearchPaperId = researchPaperId,
                SourceUrl = paper.PdfUrl,
                ExternalSource = DetectSource(paper.PdfUrl),
                Status = PaperDownloadStatus.Queued,
                AnalysisStatus = PdfAnalysisStatus.Pending
            };
            await _pdfFileRepo.AddAsync(pdfFile);
            await _pdfFileRepo.SaveChangesAsync();
            _logger.LogInformation("Created PaperPdfFile entry for paper {Id} via orchestrator", researchPaperId);
        }

        var ok = await EnsureLocalPdfAsync(pdfFile, ct);

        // Re-fetch từ DB để có state mới nhất
        var refreshed = await _pdfFileRepo.GetByResearchPaperIdAsync(researchPaperId);
        return refreshed;
    }

    public async Task<bool> EnsureLocalPdfAsync(PaperPdfFile pdfFile, CancellationToken ct)
    {
        // 1. Đã có file Ready trên storage? Tránh tải lại.
        if (pdfFile.Status == PaperDownloadStatus.Ready && !string.IsNullOrEmpty(pdfFile.LocalRelativePath))
        {
            if (await FileExistsOnStorageAsync(pdfFile))
            {
                _logger.LogInformation("PDF already Ready on storage for paper {Id}: {Rel}",
                    pdfFile.ResearchPaperId, pdfFile.LocalRelativePath);
                return true;
            }
            _logger.LogWarning("PaperPdfFile #{Id} marked Ready but file missing on storage — re-downloading",
                pdfFile.Id);
        }

        if (string.IsNullOrWhiteSpace(pdfFile.SourceUrl))
        {
            pdfFile.FailureReason = "SourceUrl is empty";
            pdfFile.Status = PaperDownloadStatus.Failed;
            _pdfFileRepo.Update(pdfFile);
            await _pdfFileRepo.SaveChangesAsync();
            return false;
        }

        // 2. Mark Downloading
        pdfFile.Status = PaperDownloadStatus.Downloading;
        pdfFile.AttemptCount += 1;
        _pdfFileRepo.Update(pdfFile);
        await _pdfFileRepo.SaveChangesAsync();

        // 3. Download bytes
        DownloadedDocument? doc;
        try
        {
            doc = await _downloader.DownloadAsync(pdfFile.SourceUrl, ct);
        }
        catch (Exception ex)
        {
            pdfFile.FailureReason = $"Downloader threw {ex.GetType().Name}: {ex.Message}";
            pdfFile.Status = PaperDownloadStatus.Failed;
            pdfFile.CompletedAt = DateTime.UtcNow;
            _pdfFileRepo.Update(pdfFile);
            await _pdfFileRepo.SaveChangesAsync();
            _logger.LogWarning(ex, "PDF download threw for paper {Id}", pdfFile.ResearchPaperId);
            return false;
        }

        if (doc == null || doc.Bytes == null || doc.Bytes.Length == 0)
        {
            pdfFile.FailureReason = $"Downloader returned null/empty. URL: {pdfFile.SourceUrl}";
            pdfFile.Status = PaperDownloadStatus.Failed;
            pdfFile.CompletedAt = DateTime.UtcNow;
            _pdfFileRepo.Update(pdfFile);
            await _pdfFileRepo.SaveChangesAsync();
            _logger.LogWarning("Download returned null/empty for paper {Id}", pdfFile.ResearchPaperId);
            return false;
        }

        // 4. *** KEY FIX: Validate đầy đủ (URL safety + size + magic-bytes) ***
        var validationError = PdfValidationHelper.ValidateDownloadedPdf(
            pdfFile.SourceUrl,
            doc.Bytes,
            doc.ContentType);

        if (validationError != null)
        {
            pdfFile.FailureReason = validationError;
            pdfFile.SizeBytes = doc.Bytes.LongLength;
            pdfFile.ContentType = doc.ContentType;
            pdfFile.Status = PaperDownloadStatus.Failed;
            pdfFile.CompletedAt = DateTime.UtcNow;
            _pdfFileRepo.Update(pdfFile);
            await _pdfFileRepo.SaveChangesAsync();

            _logger.LogWarning(
                "PDF validation FAILED for paper {Id}: {Error}. URL: {Url}. Bytes: {Size:N0}. ContentType: {CT}",
                pdfFile.ResearchPaperId,
                validationError,
                pdfFile.SourceUrl,
                doc.Bytes.LongLength,
                doc.ContentType ?? "(null)");
            return false;
        }

        // 5. Save to storage + update entity
        var relativePath = $"papers/{pdfFile.ResearchPaperId}.pdf";
        try
        {
            await _storageProvider.GetActiveStorage().SaveBytesAsync(relativePath, doc.Bytes, ct);
        }
        catch (Exception ex)
        {
            pdfFile.FailureReason = $"Storage save threw {ex.GetType().Name}: {ex.Message}";
            pdfFile.Status = PaperDownloadStatus.Failed;
            pdfFile.CompletedAt = DateTime.UtcNow;
            _pdfFileRepo.Update(pdfFile);
            await _pdfFileRepo.SaveChangesAsync();
            _logger.LogWarning(ex, "Storage save failed for paper {Id}", pdfFile.ResearchPaperId);
            return false;
        }

        var sha256 = ComputeSha256(doc.Bytes);

        pdfFile.LocalRelativePath = relativePath;
        pdfFile.SizeBytes = doc.Bytes.LongLength;
        pdfFile.ContentType = doc.ContentType ?? "application/pdf";
        pdfFile.Sha256 = sha256;
        pdfFile.Status = PaperDownloadStatus.Ready;
        pdfFile.FailureReason = null;
        pdfFile.CompletedAt = DateTime.UtcNow;

        _pdfFileRepo.Update(pdfFile);
        await _pdfFileRepo.SaveChangesAsync();

        _logger.LogInformation(
            "PDF downloaded & validated for paper {Id}. Size={Size:N0} bytes, sha={Sha8}",
            pdfFile.ResearchPaperId,
            doc.Bytes.LongLength,
            sha256[..Math.Min(8, sha256.Length)]);

        return true;
    }

    /// <summary>
    /// Check file có thật sự tồn tại trên storage hiện tại không.
    /// Local storage: check File.Exists. B2: bỏ qua (file sẽ fail downstream nếu thật sự mất).
    /// </summary>
    private async Task<bool> FileExistsOnStorageAsync(PaperPdfFile pdfFile)
    {
        var storage = _storageProvider.GetActiveStorage();
        var storageTypeName = storage.GetType().Name;

        if (storageTypeName == "LocalPaperFileStorage")
        {
            var localPath = storage.ResolveAbsolutePath(pdfFile.LocalRelativePath!);
            return await Task.FromResult(System.IO.File.Exists(localPath));
        }

        // B2: assume exists (sẽ fail downstream nếu thật sự mất)
        await Task.CompletedTask;
        return true;
    }

    private static string ComputeSha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string DetectSource(string url)
    {
        var lower = url.ToLowerInvariant();
        if (lower.Contains("arxiv.org")) return PaperDownloadStatus.AccessTypes.ArXiv;
        if (lower.Contains("openaccess") || lower.Contains("doi.org")) return PaperDownloadStatus.AccessTypes.OpenAccess;
        return PaperDownloadStatus.AccessTypes.Publisher;
    }
}
