using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.Aggregation;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.Services.Aggregation;
using ScholarTrend.Domain.Constants;

namespace ScholarTrend.Infrastructure.ExternalApis;

public class OpenAlexClient : IOpenAlexClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAlexClient> _logger;
    private readonly string _searchQuery;
    private readonly string _politeEmail;

    public OpenAlexClient(HttpClient httpClient, IConfiguration configuration, ILogger<OpenAlexClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _searchQuery = configuration["ExternalApis:OpenAlex:SearchQuery"] ?? "machine learning";
        _politeEmail = configuration["ExternalApis:OpenAlex:PoliteEmail"] ?? "support@scholartrend.com";

        var baseUrl = configuration["ExternalApis:OpenAlex:BaseUrl"] ?? "https://api.openalex.org";
        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    }

    public async Task<IReadOnlyList<ExternalPaperDto>> SearchPapersAsync(string query, int limit = 20)
    {
        var searchTerm = string.IsNullOrWhiteSpace(query) ? _searchQuery : query;
        var url = $"works?search={Uri.EscapeDataString(searchTerm)}&per-page={limit}&mailto={Uri.EscapeDataString(_politeEmail)}&select=id,display_name,publication_year,cited_by_count,doi,abstract_inverted_index,authorships,primary_location,open_access,keywords,primary_topic,topics";

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<OpenAlexSearchResponse>(url);
                if (response?.Results == null)
                {
                    return [];
                }

                return response.Results.Select(w =>
                {
                    var pdfUrl = w.OpenAccess?.Url;
                    var isOa = w.OpenAccess?.IsOa == true;
                    return new ExternalPaperDto
                    {
                        ExternalId = w.Id ?? string.Empty,
                        Source = "OpenAlex",
                        Title = w.DisplayName ?? "Untitled",
                        Abstract = w.AbstractInvertedIndex == null ? null : string.Join(" ", w.AbstractInvertedIndex.Keys),
                        Year = w.PublicationYear,
                        CitationCount = w.CitedByCount,
                        Doi = w.Doi,
                        Url = w.Id,
                        AuthorNames = w.Authorships?.Select(a => a.Author?.DisplayName ?? "Unknown").ToList() ?? [],
                        Journal = w.PrimaryLocation?.Source?.DisplayName,
                        Keywords = w.Keywords?.Select(k => k.DisplayName ?? string.Empty)
                            .Where(k => !string.IsNullOrWhiteSpace(k)).ToList() ?? [],
                        Topics = MapTopics(w),
                        PdfUrl = pdfUrl,
                        PdfAccessType = pdfUrl is null
                            ? null
                            : isOa
                                ? PaperDownloadStatus.AccessTypes.OpenAccess
                                : PaperDownloadStatus.AccessTypes.Closed,
                        PdfLicense = null
                    };
                }).Where(p => !string.IsNullOrWhiteSpace(p.ExternalId)).ToList();
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("OpenAlex rate limited (429). Waiting 30s before retry (attempt {Attempt}/3)", attempt);
                if (attempt >= 3) break;
                await Task.Delay(TimeSpan.FromSeconds(30));
            }
            catch (Exception ex) when (attempt < 3)
            {
                _logger.LogWarning(ex, "OpenAlex request failed (attempt {Attempt})", attempt);
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAlex request failed");
                throw new InvalidOperationException("Failed to fetch papers from OpenAlex.", ex);
            }
        }

        return [];
    }

    public async Task<PaperSourceMetadataDto> GetByDoiAsync(string doi)
    {
        var normalizedDoi = MetadataMapper.NormalizeDoi(doi);
        if (string.IsNullOrWhiteSpace(normalizedDoi))
        {
            return MetadataMapper.NotFound("openalex", "DOI is required.");
        }

        var url = $"works/https://doi.org/{Uri.EscapeDataString(normalizedDoi)}?mailto={Uri.EscapeDataString(_politeEmail)}";

        try
        {
            var work = await _httpClient.GetFromJsonAsync<OpenAlexWork>(url);
            if (work == null || string.IsNullOrWhiteSpace(work.Id))
            {
                return MetadataMapper.NotFound("openalex", "No OpenAlex record found.");
            }

            var pdfUrl = work.OpenAccess?.Url ?? work.PrimaryLocation?.PdfUrl;
            var isOa = work.OpenAccess?.IsOa == true;
            var external = new ExternalPaperDto
            {
                ExternalId = work.Id,
                Source = "openalex",
                Title = work.DisplayName ?? "Untitled",
                Abstract = work.AbstractInvertedIndex == null ? null : string.Join(" ", work.AbstractInvertedIndex.Keys),
                Year = work.PublicationYear,
                CitationCount = work.CitedByCount,
                Doi = work.Doi,
                Url = work.PrimaryLocation?.LandingPageUrl ?? work.Doi ?? work.Id,
                Journal = work.PrimaryLocation?.Source?.DisplayName,
                AuthorNames = work.Authorships?.Select(a => a.Author?.DisplayName ?? "Unknown").ToList() ?? [],
                Keywords = work.Keywords?.Select(k => k.DisplayName ?? string.Empty).Where(k => !string.IsNullOrWhiteSpace(k)).ToList() ?? [],
                Topics = MapTopics(work),
                PdfUrl = pdfUrl,
                PdfAccessType = pdfUrl is null
                    ? null
                    : isOa
                        ? PaperDownloadStatus.AccessTypes.OpenAccess
                        : PaperDownloadStatus.AccessTypes.Closed,
                PdfLicense = work.OpenAccess?.OaStatus,
                PublicationType = work.Type
            };

            return MetadataMapper.FromExternal(external, "openalex");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return MetadataMapper.NotFound("openalex", "No OpenAlex record found.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAlex DOI lookup failed for {Doi}", normalizedDoi);
            return MetadataMapper.NotFound("openalex", "Failed to fetch metadata from OpenAlex.");
        }
    }

    private sealed class OpenAlexSearchResponse
    {
        [JsonPropertyName("results")]
        public List<OpenAlexWork>? Results { get; set; }
    }

    private sealed class OpenAlexWork
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("publication_year")]
        public int? PublicationYear { get; set; }

        [JsonPropertyName("cited_by_count")]
        public int? CitedByCount { get; set; }

        [JsonPropertyName("doi")]
        public string? Doi { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("abstract_inverted_index")]
        public Dictionary<string, int[]>? AbstractInvertedIndex { get; set; }

        [JsonPropertyName("authorships")]
        public List<OpenAlexAuthorship>? Authorships { get; set; }

        [JsonPropertyName("primary_location")]
        public OpenAlexLocation? PrimaryLocation { get; set; }

        [JsonPropertyName("keywords")]
        public List<OpenAlexKeyword>? Keywords { get; set; }

        [JsonPropertyName("primary_topic")]
        public OpenAlexTopic? PrimaryTopic { get; set; }

        [JsonPropertyName("topics")]
        public List<OpenAlexTopic>? Topics { get; set; }

        [JsonPropertyName("open_access")]
        public OpenAlexOpenAccess? OpenAccess { get; set; }
    }

    private sealed class OpenAlexLocation
    {
        [JsonPropertyName("landing_page_url")]
        public string? LandingPageUrl { get; set; }

        [JsonPropertyName("pdf_url")]
        public string? PdfUrl { get; set; }

        [JsonPropertyName("source")]
        public OpenAlexSource? Source { get; set; }
    }

    private sealed class OpenAlexSource
    {
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }
    }

    private sealed class OpenAlexKeyword
    {
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }
    }

    private sealed class OpenAlexTopic
    {
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("field")]
        public OpenAlexTopicNode? Field { get; set; }

        [JsonPropertyName("subfield")]
        public OpenAlexTopicNode? Subfield { get; set; }

        [JsonPropertyName("domain")]
        public OpenAlexTopicNode? Domain { get; set; }
    }

    private sealed class OpenAlexTopicNode
    {
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }
    }

    private sealed class OpenAlexOpenAccess
    {
        [JsonPropertyName("oa_url")]
        public string? Url { get; set; }

        [JsonPropertyName("is_oa")]
        public bool? IsOa { get; set; }

        [JsonPropertyName("oa_status")]
        public string? OaStatus { get; set; }
    }

    private static List<string> MapTopics(OpenAlexWork work)
    {
        var labels = new List<string>();

        void Add(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                labels.Add(value.Trim());
            }
        }

        if (work.PrimaryTopic != null)
        {
            Add(work.PrimaryTopic.DisplayName);
            Add(work.PrimaryTopic.Subfield?.DisplayName);
            Add(work.PrimaryTopic.Field?.DisplayName);
            Add(work.PrimaryTopic.Domain?.DisplayName);
        }

        foreach (var topic in work.Topics ?? [])
        {
            Add(topic.DisplayName);
            Add(topic.Field?.DisplayName);
        }

        return labels
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
    }

    private sealed class OpenAlexAuthorship
    {
        [JsonPropertyName("author")]
        public OpenAlexAuthor? Author { get; set; }
    }

    private sealed class OpenAlexAuthor
    {
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }
    }
}
