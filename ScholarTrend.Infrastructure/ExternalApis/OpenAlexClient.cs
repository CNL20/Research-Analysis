using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.Interfaces.External;

namespace ScholarTrend.Infrastructure.ExternalApis;

public class OpenAlexClient : IOpenAlexClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAlexClient> _logger;
    private readonly string _searchQuery;

    public OpenAlexClient(HttpClient httpClient, IConfiguration configuration, ILogger<OpenAlexClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _searchQuery = configuration["ExternalApis:OpenAlex:SearchQuery"] ?? "machine learning";

        var baseUrl = configuration["ExternalApis:OpenAlex:BaseUrl"] ?? "https://api.openalex.org";
        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    }

    public async Task<IReadOnlyList<ExternalPaperDto>> SearchPapersAsync(string query, int limit = 20)
    {
        var searchTerm = string.IsNullOrWhiteSpace(query) ? _searchQuery : query;
        var url = $"works?search={Uri.EscapeDataString(searchTerm)}&per-page={limit}";

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<OpenAlexSearchResponse>(url);
                if (response?.Results == null)
                {
                    return [];
                }

                return response.Results.Select(w => new ExternalPaperDto
                {
                    ExternalId = w.Id ?? string.Empty,
                    Source = "OpenAlex",
                    Title = w.DisplayName ?? "Untitled",
                    Abstract = w.AbstractInvertedIndex == null ? null : string.Join(" ", w.AbstractInvertedIndex.Keys),
                    Year = w.PublicationYear,
                    CitationCount = w.CitedByCount,
                    Doi = w.Doi,
                    Url = w.Id,
                    AuthorNames = w.Authorships?.Select(a => a.Author?.DisplayName ?? "Unknown").ToList() ?? []
                }).Where(p => !string.IsNullOrWhiteSpace(p.ExternalId)).ToList();
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

        [JsonPropertyName("abstract_inverted_index")]
        public Dictionary<string, int[]>? AbstractInvertedIndex { get; set; }

        [JsonPropertyName("authorships")]
        public List<OpenAlexAuthorship>? Authorships { get; set; }
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
