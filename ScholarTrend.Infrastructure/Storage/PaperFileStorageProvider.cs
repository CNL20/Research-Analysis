using Microsoft.Extensions.Configuration;
using ScholarTrend.Application.Interfaces;

namespace ScholarTrend.Infrastructure.Storage;

/// <summary>
/// Triển khai <see cref="IPaperFileStorageProvider"/>: chọn implementation theo
/// FileUpload:Provider config ("B2" hoặc "Local").
/// </summary>
public class PaperFileStorageProvider : IPaperFileStorageProvider
{
    private readonly IPaperFileStorage _storage;

    public PaperFileStorageProvider(
        IConfiguration configuration,
        LocalPaperFileStorage local,
        B2PaperFileStorage b2)
    {
        var provider = configuration["FileUpload:Provider"] ?? "Local";
        _storage = string.Equals(provider, "B2", StringComparison.OrdinalIgnoreCase)
            ? b2
            : local;
    }

    public IPaperFileStorage GetActiveStorage() => _storage;
}