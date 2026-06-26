using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.Aggregation;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.Services.Aggregation;

namespace ScholarTrend.Infrastructure.ExternalApis;

public class SemanticScholarClient : ISemanticScholarClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SemanticScholarClient> _logger;
    private readonly string _searchQuery;
    private readonly int _pageSize;

    public SemanticScholarClient(HttpClient httpClient, IConfiguration configuration, ILogger<SemanticScholarClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _searchQuery = configuration["ExternalApis:SemanticScholar:SearchQuery"] ?? "artificial intelligence";
        _pageSize = int.TryParse(configuration["ExternalApis:SemanticScholar:PageSize"], out var size) ? size : 10;

        var baseUrl = configuration["ExternalApis:SemanticScholar:BaseUrl"] ?? "https://api.semanticscholar.org/graph/v1";
        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    }

    public async Task<IReadOnlyList<ExternalPaperDto>> SearchPapersAsync(string query, int limit = 20)
    {
        var searchTerm = string.IsNullOrWhiteSpace(query) ? _searchQuery : query;
        var take = limit > 0 ? limit : _pageSize;
        var url = $"paper/search?query={Uri.EscapeDataString(searchTerm)}&limit={take}&fields=title,abstract,year,citationCount,url,externalIds,authors.name";

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<SemanticScholarSearchResponse>(url);
                if (response?.Data == null)
                {
                    return [];
                }

                return response.Data.Select(p => new ExternalPaperDto
                {
                    ExternalId = p.PaperId ?? string.Empty,
                    Source = "SemanticScholar",
                    Title = p.Title ?? "Untitled",
                    Abstract = p.Abstract,
                    Year = p.Year,
                    CitationCount = p.CitationCount,
                    Doi = p.ExternalIds?.Doi,
                    Url = p.Url,
                    AuthorNames = p.Authors?.Select(a => a.Name ?? "Unknown").ToList() ?? []
                }).Where(p => !string.IsNullOrWhiteSpace(p.ExternalId)).ToList();
            }
            catch (Exception ex) when (attempt < 3)
            {
                _logger.LogWarning(ex, "Semantic Scholar request failed (attempt {Attempt})", attempt);
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Semantic Scholar request failed");
                throw new InvalidOperationException("Failed to fetch papers from Semantic Scholar.", ex);
            }
        }

        return [];
    }

    public async Task<PaperSourceMetadataDto> GetByDoiAsync(string doi)
    {
        var normalizedDoi = MetadataMapper.NormalizeDoi(doi);
        if (string.IsNullOrWhiteSpace(normalizedDoi))
        {
            return MetadataMapper.NotFound("semantic_scholar", "DOI is required.");
        }

        var url = $"paper/DOI:{Uri.EscapeDataString(normalizedDoi)}?fields=title,abstract,year,citationCount,url,externalIds,authors.name,journal";

        try
        {
            var paper = await _httpClient.GetFromJsonAsync<SemanticScholarPaper>(url);
            if (paper == null || string.IsNullOrWhiteSpace(paper.PaperId))
            {
                return MetadataMapper.NotFound("semantic_scholar", "No Semantic Scholar record found.");
            }

            var external = new ExternalPaperDto
            {
                ExternalId = paper.PaperId,
                Source = "semantic_scholar",
                Title = paper.Title ?? "Untitled",
                Abstract = paper.Abstract,
                Year = paper.Year,
                CitationCount = paper.CitationCount,
                Doi = paper.ExternalIds?.Doi ?? normalizedDoi,
                Url = paper.Url,
                Journal = paper.Journal?.Name,
                AuthorNames = paper.Authors?.Select(a => a.Name ?? "Unknown").ToList() ?? [],
            };

            return MetadataMapper.FromExternal(external, "semantic_scholar");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return MetadataMapper.NotFound("semantic_scholar", "No Semantic Scholar record found.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Semantic Scholar DOI lookup failed for {Doi}", normalizedDoi);
            return MetadataMapper.NotFound("semantic_scholar", "Failed to fetch metadata from Semantic Scholar.");
        }
    }

    private sealed class SemanticScholarSearchResponse
    {
        [JsonPropertyName("data")]
        public List<SemanticScholarPaper>? Data { get; set; }
    }

    private sealed class SemanticScholarPaper
    {
        [JsonPropertyName("paperId")]
        public string? PaperId { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("abstract")]
        public string? Abstract { get; set; }

        [JsonPropertyName("year")]
        public int? Year { get; set; }

        [JsonPropertyName("citationCount")]
        public int? CitationCount { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("externalIds")]
        public SemanticScholarExternalIds? ExternalIds { get; set; }

        [JsonPropertyName("authors")]
        public List<SemanticScholarAuthor>? Authors { get; set; }

        [JsonPropertyName("journal")]
        public SemanticScholarJournal? Journal { get; set; }
    }

    private sealed class SemanticScholarJournal
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private sealed class SemanticScholarAuthor
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private sealed class SemanticScholarExternalIds
    {
        [JsonPropertyName("DOI")]
        public string? Doi { get; set; }
    }
}
