namespace ScholarTrend.Application.Interfaces;

/// <summary>
/// Trích xuất text từ PDF — abstraction layer để hỗ trợ nhiều PDF library
/// (PdfPig, iTextSharp, hoặc gọi external service) mà không phải đổi consumer code.
///
/// Lý do cần abstraction:
///   - IPaperFileStorage.OpenReadAsync() trả Stream (không có file path thật cho B2).
///   - PdfPig chấp nhận Stream input (PdfDocument.Open(Stream)) — không cần lưu xuống disk.
///   - Local storage có file path thật — extract nhanh hơn (tránh đọc toàn bộ vào memory).
/// </summary>
public interface IPaperTextExtractor
{
    /// <summary>
    /// Extract text từ PDF stream. Trả về text đã được normalize (single newline).
    /// Trả về null nếu stream rỗng hoặc không phải PDF hợp lệ.
    /// </summary>
    /// <param name="pdfStream">Stream PDF bytes (B2 download hoặc local file stream).</param>
    /// <param name="sourceLabel">Tên file/identifier để log debug.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<string?> ExtractTextAsync(Stream pdfStream, string sourceLabel, CancellationToken ct = default);

    /// <summary>
    /// Extract text từ file PDF trên disk (chỉ dùng cho LocalPaperFileStorage —
    /// PdfPig đọc trực tiếp từ file path, không tốn memory).
    /// </summary>
    Task<string?> ExtractTextFromFileAsync(string filePath, CancellationToken ct = default);
}