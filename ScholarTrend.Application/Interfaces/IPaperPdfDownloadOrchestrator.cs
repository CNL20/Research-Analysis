using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces;

/// <summary>
/// Orchestrator trung gian cho việc tải PDF on-demand (synchronous path).
/// Tách ra từ GeminiPdfAnalysisService để:
///   -1. Validate PDF đầy đủ (magic-bytes + URL safety) trước khi lưu.
///   -2. Đảm bảo PaperPdfFile entity được update đúng status (Queued/Downloading/Ready/Failed).
///   -3. Cho phép cả channel path (PaperPdfDownloadService) và on-demand path (GeminiPdfAnalysisService)
///       dùng chung logic download + validation.
///
/// Đây chính là single source of truth cho PDF download, thay vì duplicate logic
/// giữa 2 services.
/// </summary>
public interface IPaperPdfDownloadOrchestrator
{
    /// <summary>
    /// Đảm bảo paper có PDF sẵn sàng trên storage. Nếu chưa có, sẽ download
    /// + validate đầy đủ + save.
    ///
    /// Returns true nếu PDF ready (đã có hoặc mới tải thành công).
    /// Returns false nếu download/validation fail.
    /// </summary>
    /// <param name="pdfFile">Entity PaperPdfFile. Trước khi gọi đảm bảo đã có trong DB.</param>
    /// <param name="ct">Cancellation token</param>
    Task<bool> EnsureLocalPdfAsync(PaperPdfFile pdfFile, CancellationToken ct);

    /// <summary>
    /// Variant: tự tạo PaperPdfFile nếu chưa có, rồi đảm bảo PDF.
    /// Idempotent — gọi nhiều lần với cùng researchPaperId đều an toàn.
    /// Returns null nếu fail (paper không tồn tại, không có URL, hoặc download fail).
    /// </summary>
    Task<PaperPdfFile?> EnsurePdfForPaperAsync(int researchPaperId, CancellationToken ct);
}
