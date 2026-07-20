namespace ScholarTrend.Application.Interfaces;

public interface IPaperFileStorage
{
    /// <summary>
    /// Lưu binary bytes xuống disk tại đường dẫn tương đối (vd "papers/123.pdf").
    /// Trả về đường dẫn tuyệt đối trên disk.
    /// </summary>
    Task<string> SaveBytesAsync(string relativePath, byte[] bytes, CancellationToken ct);

    /// <summary>
    /// Đường dẫn tuyệt đối trên disk tương ứng với relativePath.
    /// </summary>
    string ResolveAbsolutePath(string relativePath);

    /// <summary>
    /// Xoá file nếu tồn tại (best-effort).
    /// </summary>
    void DeleteIfExists(string relativePath);

    /// <summary>
    /// Mở file để đọc qua stream. Trả về null nếu file không tồn tại.
    /// Caller phải dispose stream sau khi dùng xong.
    /// </summary>
    /// <remarks>
    /// Với local disk: trả FileStream trực tiếp.
    /// Với B2 (private bucket): download về MemoryStream rồi trả (proxy qua backend).
    /// </remarks>
    Task<Stream?> OpenReadAsync(string relativePath, CancellationToken ct);

    /// <summary>
    /// Đọc toàn bộ PDF bytes vào memory. Trả về null nếu file không tồn tại.
    /// Dùng cho PDF text extraction hoặc upload lại — khi cần random-access (PdfPig)
    /// hoặc pass toàn bộ bytes cho API khác.
    /// </summary>
    /// <remarks>
    /// Lưu ý: Với PDF &gt; 50 MB, nên dùng OpenReadAsync + MemoryStream thay vì
    /// load toàn bộ vào byte[] (tránh OOM).
    /// </remarks>
    Task<byte[]?> ReadAllBytesAsync(string relativePath, CancellationToken ct);
}
