using System.Net.Http.Json;

using System.Text.Json.Serialization;

using Microsoft.Extensions.Configuration;

using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.Aggregation;
using ScholarTrend.Application.Interfaces.External;

using ScholarTrend.Application.Services.Aggregation;

using ScholarTrend.Domain.Constants;



namespace ScholarTrend.Infrastructure.ExternalApis;



public class CrossrefClient : ICrossrefClient

{

    private readonly HttpClient _httpClient;

    private readonly ILogger<CrossrefClient> _logger;

    private readonly string _searchQuery;



    public CrossrefClient(HttpClient httpClient, IConfiguration configuration, ILogger<CrossrefClient> logger)

    {

        _httpClient = httpClient;

        _logger = logger;

        _searchQuery = configuration["ExternalApis:Crossref:SearchQuery"] ?? "artificial intelligence";

        var baseUrl = configuration["ExternalApis:Crossref:BaseUrl"] ?? "https://api.crossref.org";

        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");

        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ScholarTrend/1.0 (mailto:support@scholartrend.local)");

    }



    public async Task<IReadOnlyList<ExternalPaperDto>> SearchPapersAsync(string query, int limit = 20)

    {

        var searchTerm = string.IsNullOrWhiteSpace(query) ? _searchQuery : query;

        var rows = Math.Clamp(limit, 1, 100);

        var url = $"works?query={Uri.EscapeDataString(searchTerm)}&rows={rows}&sort=published&order=desc";



        for (var attempt = 1; attempt <= 3; attempt++)

        {

            try

            {

                var response = await _httpClient.GetFromJsonAsync<CrossrefSearchResponse>(url);

                var items = response?.Message?.Items ?? [];

                return items

                    .Select(MapToExternalPaper)

                    .Where(p => !string.IsNullOrWhiteSpace(p.ExternalId))

                    .ToList();

            }

            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)

            {

                _logger.LogWarning("Crossref rate limited (429). Waiting 30s before retry (attempt {Attempt}/3)", attempt);

                if (attempt >= 3) break;

                await Task.Delay(TimeSpan.FromSeconds(30));

            }

            catch (Exception ex) when (attempt < 3)

            {

                _logger.LogWarning(ex, "Crossref search failed (attempt {Attempt})", attempt);

                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Crossref search failed");

                throw new InvalidOperationException("Failed to fetch papers from Crossref.", ex);

            }

        }



        return [];

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

            if (response?.Message == null)

            {

                return MetadataMapper.NotFound("crossref", "No Crossref record found.");

            }



            return MetadataMapper.FromExternal(MapToExternalPaper(response.Message), "crossref");

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



    private static ExternalPaperDto MapToExternalPaper(CrossrefWork work)

    {

        var doi = MetadataMapper.NormalizeDoi(work.Doi);

        var title = work.Title?.FirstOrDefault() ?? "Untitled";

        var journal = work.ContainerTitle?.FirstOrDefault();

        var year = work.Published?.DateParts?.FirstOrDefault()?.FirstOrDefault()

                   ?? work.Created?.DateParts?.FirstOrDefault()?.FirstOrDefault();

        var authors = ResolveContributors(work);



        var pdfLink = work.Link?
            .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l.Url)
                && (string.Equals(l.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
                    || l.Url.Contains(".pdf", StringComparison.OrdinalIgnoreCase)));

        var pdfUrl = pdfLink?.Url;

        var pdfAccessType = pdfUrl is null

            ? null

            : PaperDownloadStatus.AccessTypes.Publisher;



        return new ExternalPaperDto

        {

            ExternalId = doi ?? work.Doi ?? title,

            Source = "Crossref",

            Title = title,

            Abstract = work.Abstract,

            Year = year,

            CitationCount = work.IsReferencedByCount,

            Doi = doi,

            Url = string.IsNullOrWhiteSpace(doi) ? null : $"https://doi.org/{doi}",

            Journal = journal,

            AuthorNames = authors,

            Keywords = work.Subject?

                .Where(s => !string.IsNullOrWhiteSpace(s))

                .Distinct(StringComparer.OrdinalIgnoreCase)

                .ToList() ?? [],

            Topics = work.Subject?

                .Where(s => !string.IsNullOrWhiteSpace(s))

                .Distinct(StringComparer.OrdinalIgnoreCase)

                .Take(5)

                .ToList() ?? [],

            PdfUrl = pdfUrl,

            PdfAccessType = pdfAccessType,

            PdfLicense = work.License?.FirstOrDefault()?.Url,

            PublicationType = work.Type

        };

    }



    private static List<string> ResolveContributors(CrossrefWork work)

    {

        var authors = MapContributors(work.Author);

        if (authors.Count > 0)

        {

            return authors;

        }



        return MapContributors(work.Editor);

    }



    private static List<string> MapContributors(List<CrossrefAuthor>? contributors)

    {

        return contributors?

            .Select(a => string.Join(' ', new[] { a.Given, a.Family }.Where(x => !string.IsNullOrWhiteSpace(x))))

            .Where(name => !string.IsNullOrWhiteSpace(name))

            .Select(name => name!)

            .ToList() ?? [];

    }



    private sealed class CrossrefSearchResponse

    {

        [JsonPropertyName("message")]

        public CrossrefSearchMessage? Message { get; set; }

    }



    private sealed class CrossrefSearchMessage

    {

        [JsonPropertyName("items")]

        public List<CrossrefWork>? Items { get; set; }

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



        [JsonPropertyName("type")]

        public string? Type { get; set; }



        [JsonPropertyName("title")]

        public List<string>? Title { get; set; }



        [JsonPropertyName("container-title")]

        public List<string>? ContainerTitle { get; set; }



        [JsonPropertyName("author")]

        public List<CrossrefAuthor>? Author { get; set; }



        [JsonPropertyName("editor")]

        public List<CrossrefAuthor>? Editor { get; set; }



        [JsonPropertyName("subject")]

        public List<string>? Subject { get; set; }



        [JsonPropertyName("published-print")]

        public CrossrefDateParts? Published { get; set; }



        [JsonPropertyName("created")]

        public CrossrefDateParts? Created { get; set; }



        [JsonPropertyName("abstract")]

        public string? Abstract { get; set; }



        [JsonPropertyName("is-referenced-by-count")]

        public int? IsReferencedByCount { get; set; }



        [JsonPropertyName("link")]

        public List<CrossrefLink>? Link { get; set; }



        [JsonPropertyName("license")]

        public List<CrossrefLicense>? License { get; set; }

    }



    private sealed class CrossrefLink

    {

        [JsonPropertyName("URL")]

        public string? Url { get; set; }



        [JsonPropertyName("content-type")]

        public string? ContentType { get; set; }

    }



    private sealed class CrossrefLicense

    {

        [JsonPropertyName("URL")]

        public string? Url { get; set; }

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


