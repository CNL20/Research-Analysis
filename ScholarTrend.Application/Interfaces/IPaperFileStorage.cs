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
}
