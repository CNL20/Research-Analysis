using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.Migration;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Domain.Constants;

namespace ScholarTrend.Application.Services;

/// <summary>
/// Migration tool: đọc PaperPdfFile (Status=Ready) từ DB, tải file từ local disk,
/// upload lên B2 (nếu chưa có). Idempotent — bỏ qua file đã có ở B2.
///
/// Trigger: AdminMigrationController.MigratePdfs (chỉ chạy thủ công).
/// KHÔNG chạy auto khi startup vì có thể làm chậm cold start + tốn bandwidth.
///
/// Inject IEnumerable&lt;IPaperFileStorage&gt; — DI phải đăng ký CẢ 2 qua interface
/// (cả Local lẫn B2) để service này hoạt động. Program.cs chịu trách nhiệm đảm bảo.
/// </summary>
public class PdfStorageMigrationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaperFileStorage _sourceStorage;     // local disk
    private readonly IPaperFileStorage _targetStorage;     // B2
    private readonly ILogger<PdfStorageMigrationService> _logger;

    public PdfStorageMigrationService(
        IUnitOfWork unitOfWork,
        IEnumerable<IPaperFileStorage> storages,
        ILogger<PdfStorageMigrationService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;

        var all = storages.ToList();
        if (all.Count < 2)
        {
            throw new InvalidOperationException(
                "PdfStorageMigrationService requires at least 2 IPaperFileStorage implementations. " +
                "Make sure both LocalPaperFileStorage and B2PaperFileStorage are registered. " +
                "Found: [" + string.Join(", ", all.Select(s => s.GetType().Name)) + "]");
        }

        var local = all.FirstOrDefault(s => s.GetType().Name == "LocalPaperFileStorage");
        var b2 = all.FirstOrDefault(s => s.GetType().Name == "B2PaperFileStorage");

        if (local is null || b2 is null)
        {
            throw new InvalidOperationException(
                "PdfStorageMigrationService requires both LocalPaperFileStorage and B2PaperFileStorage. " +
                "Found: [" + string.Join(", ", all.Select(s => s.GetType().Name)) + "]");
        }

        _sourceStorage = local;
        _targetStorage = b2;
    }

    /// <summary>
    /// Quét tất cả PaperPdfFile có Status=Ready, upload lên target storage nếu cần.
    /// </summary>
    public async Task<PdfMigrationResultDto> MigrateAsync(CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new PdfMigrationResultDto();

        var readyFiles = await _unitOfWork.PaperPdfFiles.GetByStatusAsync(PaperDownloadStatus.Ready, take: 1000);
        result.ScannedCount = readyFiles.Count;

        _logger.LogInformation(
            "Starting PDF migration: {Count} Ready PDFs to scan",
            readyFiles.Count);

        foreach (var pdf in readyFiles)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // 1. Resolve local path
                var localPath = _sourceStorage.ResolveAbsolutePath(pdf.LocalRelativePath);
                if (!File.Exists(localPath))
                {
                    result.SkippedCount++;
                    _logger.LogDebug(
                        "Skip paper {Id}: local file not found at {Path}",
                        pdf.ResearchPaperId, localPath);
                    continue;
                }

                // 2. Đọc bytes từ local
                var bytes = await File.ReadAllBytesAsync(localPath, ct);

                // 3. Upload lên target storage (B2)
                await _targetStorage.SaveBytesAsync(pdf.LocalRelativePath, bytes, ct);

                result.SuccessCount++;
                _logger.LogInformation(
                    "Migrated PDF for paper {Id}: {Bytes} bytes ({Path})",
                    pdf.ResearchPaperId, bytes.Length, pdf.LocalRelativePath);
            }
            catch (Exception ex)
            {
                result.FailureCount++;
                result.Failures.Add(new PdfMigrationFailureDto
                {
                    ResearchPaperId = pdf.ResearchPaperId,
                    LocalRelativePath = pdf.LocalRelativePath,
                    Reason = $"{ex.GetType().Name}: {ex.Message}"
                });
                _logger.LogError(ex,
                    "Failed to migrate PDF for paper {Id}",
                    pdf.ResearchPaperId);
            }
        }

        sw.Stop();
        result.ElapsedMs = sw.ElapsedMilliseconds;

        _logger.LogInformation(
            "PDF migration finished: {Success} ok, {Failed} failed, {Skipped} skipped in {Ms} ms",
            result.SuccessCount, result.FailureCount, result.SkippedCount, result.ElapsedMs);

        return result;
    }
}