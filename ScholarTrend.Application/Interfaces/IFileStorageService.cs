namespace ScholarTrend.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveAsync(string userId, string storedFileName, Stream content, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string userId, string storedFileName, CancellationToken cancellationToken = default);
    Task DeleteAsync(string userId, string storedFileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo URL có thời hạn để tải file (dùng cho private bucket).
    /// </summary>
    /// <param name="userId">ID của user sở hữu file.</param>
    /// <param name="storedFileName">Tên file đã được lưu.</param>
    /// <param name="expirationMinutes">Thời gian hết hạn của URL (phút).</param>
    string GetSignedUrl(string userId, string storedFileName, int expirationMinutes = 60);
}
