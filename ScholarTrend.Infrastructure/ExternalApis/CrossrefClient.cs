using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.Aggregation;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.Services.Aggregation;

namespace ScholarTrend.Infrastructure.ExternalApis;

public class CrossrefClient : ICrossrefClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CrossrefClient> _logger;

    public CrossrefClient(HttpClient httpClient, IConfiguration configuration, ILogger<CrossrefClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        var baseUrl = configuration["ExternalApis:Crossref:BaseUrl"] ?? "https://api.crossref.org";
        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ScholarTrend/1.0 (mailto:support@scholartrend.local)");
    }

    public async Task<PaperSourceMetadataDto> GetByDoiAsync(string doi)
    {
        var normalizedDoi = MetadataMapper.NormalizeDoi(doi);
        if (string.IsNullOrWhiteSpace(normalizedDoi))
        {
            return MetadataMapper.NotFound("crossref", "DOI is required.");
        }

        var url = $"works/{Uri.EscapeDataString(normalizedDoi)}";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<CrossrefResponse>(url);
            var message = response?.Message;
            if (message == null)
            {
                return MetadataMapper.NotFound("crossref", "No Crossref record found.");
            }

            var title = message.Title?.FirstOrDefault();
            var journal = message.ContainerTitle?.FirstOrDefault();
            var year = message.Published?.DateParts?.FirstOrDefault()?.FirstOrDefault()
                       ?? message.Created?.DateParts?.FirstOrDefault()?.FirstOrDefault();
            var authors = message.Author?
                .Select(a => string.Join(' ', new[] { a.Given, a.Family }.Where(x => !string.IsNullOrWhiteSpace(x))))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToList() ?? [];

            return new PaperSourceMetadataDto
            {
                Source = "crossref",
                Found = true,
                ExternalId = message.Doi ?? normalizedDoi,
                Doi = MetadataMapper.NormalizeDoi(message.Doi ?? normalizedDoi),
                Title = title,
                Year = year,
                Journal = journal,
                Authors = authors,
            };
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return MetadataMapper.NotFound("crossref", "No Crossref record found.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Crossref request failed for DOI {Doi}", normalizedDoi);
            return MetadataMapper.NotFound("crossref", "Failed to fetch metadata from Crossref.");
        }
    }

    private sealed class CrossrefResponse
    {
        [JsonPropertyName("message")]
        public CrossrefWork? Message { get; set; }
    }

    private sealed class CrossrefWork
    {
        [JsonPropertyName("DOI")]
        public string? Doi { get; set; }

        [JsonPropertyName("title")]
        public List<string>? Title { get; set; }

        [JsonPropertyName("container-title")]
        public List<string>? ContainerTitle { get; set; }

        [JsonPropertyName("author")]
        public List<CrossrefAuthor>? Author { get; set; }

        [JsonPropertyName("published-print")]
        public CrossrefDateParts? Published { get; set; }

        [JsonPropertyName("created")]
        public CrossrefDateParts? Created { get; set; }
    }

    private sealed class CrossrefAuthor
    {
        [JsonPropertyName("given")]
        public string? Given { get; set; }

        [JsonPropertyName("family")]
        public string? Family { get; set; }
    }

    private sealed class CrossrefDateParts
    {
        [JsonPropertyName("date-parts")]
        public List<List<int>>? DateParts { get; set; }
    }
}
