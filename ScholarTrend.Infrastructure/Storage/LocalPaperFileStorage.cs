using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScholarTrend.Application.Interfaces;

namespace ScholarTrend.Infrastructure.Storage;

/// <summary>
/// Implementation IPaperFileStorage dùng local disk.
/// Thư mục gốc được cấu hình qua FileUpload:StoragePath trong appsettings.json (mặc định "uploads").
/// </summary>
public class LocalPaperFileStorage : IPaperFileStorage
{
    private readonly string _rootPath;
    private readonly ILogger<LocalPaperFileStorage> _logger;

    /// <summary>
    /// Constructor production — inject IOptions để ASP.NET DI tự resolve.
    /// </summary>
    public LocalPaperFileStorage(IOptions<StorageSettings> options, ILogger<LocalPaperFileStorage> logger)
        : this(options.Value, logger)
    {
    }

    /// <summary>
    /// Constructor test — cho phép truyền trực tiếp StorageSettings mà không cần DI graph.
    /// </summary>
    public LocalPaperFileStorage(StorageSettings settings, ILogger<LocalPaperFileStorage> logger)
    {
        _logger = logger;
        var configured = string.IsNullOrWhiteSpace(settings.StoragePath) ? "uploads" : settings.StoragePath;
        _rootPath = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(Directory.GetCurrentDirectory(), configured);

        Directory.CreateDirectory(Path.Combine(_rootPath, "papers"));
    }

    public async Task<string> SaveBytesAsync(string relativePath, byte[] bytes, CancellationToken ct)
    {
        var abs = ResolveAbsolutePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(abs)!);

        await File.WriteAllBytesAsync(abs, bytes, ct);
        _logger.LogDebug("Saved PDF to {Path} ({Bytes} bytes)", abs, bytes.Length);
        return abs;
    }

    public string ResolveAbsolutePath(string relativePath)
    {
        var clean = relativePath.Replace('\\', '/').TrimStart('/');
        return Path.Combine(_rootPath, clean.Replace('/', Path.DirectorySeparatorChar));
    }

    public void DeleteIfExists(string relativePath)
    {
        try
        {
            var abs = ResolveAbsolutePath(relativePath);
            if (File.Exists(abs))
            {
                File.Delete(abs);
                _logger.LogDebug("Deleted orphan PDF {Path}", abs);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete PDF at {Path}", relativePath);
        }
    }
}

public class StorageSettings
{
    public string StoragePath { get; set; } = "uploads";
}
