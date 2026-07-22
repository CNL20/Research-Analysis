namespace ScholarTrend.Application.Options;

/// <summary>
/// Cấu hình cho PDF processing pipeline.
/// </summary>
public class PdfProcessingSettings
{
    /// <summary>
    /// Bật/tắt auto-parse sau khi download thành công.
    /// </summary>
    public bool AutoParseAfterDownload { get; set; } = false;

    /// <summary>
    /// Số lượng download concurrent tối đa.
    /// </summary>
    public int MaxConcurrentDownloads { get; set; } = 3;

    /// <summary>
    /// Số lượng parse concurrent tối đa (nên thấp hơn download vì parse tốn CPU).
    /// </summary>
    public int MaxConcurrentParsing { get; set; } = 2;

    /// <summary>
    /// Timeout cho mỗi operation parse (giây).
    /// </summary>
    public int ParseTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Giới hạn ký tự text extracted từ PDF (để tránh lưu text quá lớn vào DB).
    /// </summary>
    public int MaxTextLength { get; set; } = 150000;
}
