namespace ScholarTrend.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveAsync(string userId, string storedFileName, Stream content, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string userId, string storedFileName, CancellationToken cancellationToken = default);
    Task DeleteAsync(string userId, string storedFileName, CancellationToken cancellationToken = default);
}
