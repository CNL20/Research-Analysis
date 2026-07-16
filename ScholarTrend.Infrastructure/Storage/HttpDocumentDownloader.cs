using Microsoft.Extensions.Logging;
using ScholarTrend.Application.Interfaces;

namespace ScholarTrend.Infrastructure.Storage;

/// <summary>
/// Implementation IDocumentDownloader dùng HttpClient.
/// Tự wrap timeout 30s, User-Agent header, exception handling.
/// </summary>
public class HttpDocumentDownloader : IDocumentDownloader
{
    private const int HttpTimeoutSeconds = 30;
    private const long MaxBytes = 50L * 1024 * 1024; // 50 MB

    private readonly HttpClient _http;
    private readonly ILogger<HttpDocumentDownloader> _logger;

    public HttpDocumentDownloader(HttpClient http, ILogger<HttpDocumentDownloader> logger)
    {
        _http = http;
        _logger = logger;
        if (_http.Timeout == System.Threading.Timeout.InfiniteTimeSpan)
        {
            _http.Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds);
        }
        if (!_http.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _http.DefaultRequestHeaders.Add("User-Agent", "ScholarTrend-PdfFetcher/1.0");
        }
    }

    public async Task<DownloadedDocument?> DownloadAsync(string url, CancellationToken ct)
    {
        try
        {
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Download returned {Status} for {Url}", (int)resp.StatusCode, url);
                return null;
            }

            // Content-Length guard
            var len = resp.Content.Headers.ContentLength;
            if (len.HasValue && len.Value > MaxBytes)
            {
                _logger.LogWarning("Download {Url} rejected by size: Content-Length={Size}", url, len.Value);
                return null;
            }

            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length > MaxBytes)
            {
                _logger.LogWarning("Download {Url} actual size {Size} exceeds limit", url, bytes.Length);
                return null;
            }

            return new DownloadedDocument
            {
                Bytes = bytes,
                ContentType = resp.Content.Headers.ContentType?.MediaType
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Download exception for {Url}", url);
            return null;
        }
    }
}
