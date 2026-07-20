using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Options;

namespace ScholarTrend.Infrastructure.Storage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalFileStorageService(IHostEnvironment environment, IOptions<FileUploadSettings> options)
    {
        var storagePath = options.Value.StoragePath;
        _rootPath = Path.IsPathRooted(storagePath)
            ? storagePath
            : Path.Combine(environment.ContentRootPath, storagePath);
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(
        string userId,
        string storedFileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var userDirectory = GetUserDirectory(userId);
        Directory.CreateDirectory(userDirectory);

        var fullPath = Path.Combine(userDirectory, storedFileName);
        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fileStream, cancellationToken);
        return fullPath;
    }

    public Task<Stream> OpenReadAsync(
        string userId,
        string storedFileName,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(GetUserDirectory(userId), storedFileName);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Stored file was not found.", fullPath);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string userId, string storedFileName, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(GetUserDirectory(userId), storedFileName);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public string GetSignedUrl(string userId, string storedFileName, int expirationMinutes = 60)
    {
        // Local storage: trả về relative path, caller (controller) sẽ build URL thật.
        // Đây là no-op cho local; controller sẽ tự xử lý qua /api/Files/{fileId}/download.
        var fullPath = Path.Combine(GetUserDirectory(userId), storedFileName);
        return fullPath;
    }

    private string GetUserDirectory(string userId)
    {
        var safeUserId = string.Concat(userId.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_rootPath, safeUserId);
    }
}
