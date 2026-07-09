namespace ScholarTrend.Application.Interfaces;

/// <summary>
/// Abstraction cho việc tải tài liệu (PDF) từ URL.
/// Tách ra từ PaperPdfDownloadService để dễ test với mock.
/// </summary>
public interface IDocumentDownloader
{
    /// <summary>
    /// Tải tài liệu từ URL. Trả về null nếu HTTP không thành công.
    /// </summary>
    /// <param name="url">URL nguồn</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Bytes tải về + ContentType, hoặc null nếu fail</returns>
    Task<DownloadedDocument?> DownloadAsync(string url, CancellationToken ct);
}

public class DownloadedDocument
{
    public byte[] Bytes { get; set; } = [];
    public string? ContentType { get; set; }
}
