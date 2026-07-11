using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Services.Aggregation;

namespace ScholarTrend.Infrastructure.ExternalApis;

/// <summary>
/// Resolves an ArXiv identifier to a DOI via OpenAlex.
/// Uses OpenAlex's native arXiv lookup endpoint: GET /works/arXiv:{arxivId}.
/// Results are cached for 7 days to avoid repeating network calls for the same id.
/// </summary>
public class ArxivDoiResolver : IArxivDoiResolver
{
    private const string OpenAlexBaseUrl = "https://api.openalex.org";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(7);
    private static readonly TimeSpan NegativeCacheDuration = TimeSpan.FromHours(6);
    private static readonly Regex ArxivIdRegex = new(@"(\d{4}\.\d{4,5})(v\d+)?", RegexOptions.Compiled);

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ArxivDoiResolver> _logger;

    public ArxivDoiResolver(HttpClient http, IMemoryCache cache, ILogger<ArxivDoiResolver> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
        if (_http.BaseAddress == null)
        {
            _http.BaseAddress = new Uri(OpenAlexBaseUrl + "/");
        }
        if (_http.Timeout == TimeSpan.Zero || _http.Timeout > TimeSpan.FromSeconds(30))
        {
            _http.Timeout = TimeSpan.FromSeconds(15);
        }
    }

    public async Task<string?> ResolveDoiAsync(string arxivId, CancellationToken ct = default)
    {
        var cleanedId = ExtractArxivId(arxivId);
        if (string.IsNullOrEmpty(cleanedId))
        {
            return null;
        }

        var cacheKey = $"arxiv-doi:{cleanedId}";
        if (_cache.TryGetValue<string?>(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            var url = $"works/arXiv:{Uri.EscapeDataString(cleanedId)}";
            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _cache.Set(cacheKey, (string?)null, NegativeCacheDuration);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            string? doi = null;
            if (doc.RootElement.TryGetProperty("doi", out var doiEl))
            {
                var raw = doiEl.GetString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    doi = MetadataMapper.NormalizeDoi(raw);
                }
            }

            _cache.Set(cacheKey, doi, CacheDuration);
            return doi;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ArXiv->DOI lookup failed for {ArxivId}", arxivId);
            return null;
        }
    }

    private static string ExtractArxivId(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var match = ArxivIdRegex.Match(raw);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }
}
