using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.Aggregation;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.Services.Aggregation;
using ScholarTrend.Application.Services.Keywords;
using ScholarTrend.Domain.Constants;

namespace ScholarTrend.Infrastructure.ExternalApis;

public class ArXivClient : IArXivClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ArXivClient> _logger;
    private readonly string _searchQuery;

    public ArXivClient(HttpClient httpClient, IConfiguration configuration, ILogger<ArXivClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _searchQuery = configuration["ExternalApis:ArXiv:SearchQuery"] ?? "artificial intelligence";

        var baseUrl = configuration["ExternalApis:ArXiv:BaseUrl"] ?? "https://export.arxiv.org/api/query";
        // ArXiv API rejects trailing slash before "?" — use raw URL and prefix url with "query?"
        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/'));
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        var rawEmail = configuration["ExternalApis:OpenAlex:PoliteEmail"] ?? "admin@scholartrend.com";
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"ScholarTrend/1.0 (mailto:{rawEmail})");
    }

    public async Task<IReadOnlyList<ExternalPaperDto>> SearchPapersAsync(string query, int limit = 20)
    {
        var searchTerm = string.IsNullOrWhiteSpace(query) ? _searchQuery : query;
        var url = $"query?search_query=all:{Uri.EscapeDataString(searchTerm)}&start=0&max_results={limit}&sortBy=submittedDate&sortOrder=descending";

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var response = await _httpClient.GetStringAsync(url);
                return ParseAtomFeed(response);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("ArXiv rate limited (429). Waiting 30s before retry (attempt {Attempt})", attempt);
                if (attempt >= 3) break;
                await Task.Delay(TimeSpan.FromSeconds(30));
            }
            catch (Exception ex) when (attempt < 3)
            {
                _logger.LogWarning(ex, "ArXiv request failed (attempt {Attempt})", attempt);
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ArXiv request failed");
                throw new InvalidOperationException("Failed to fetch papers from ArXiv.", ex);
            }
        }

        return [];
    }

    public async Task<PaperSourceMetadataDto> GetByDoiAsync(string doi)
    {
        var normalizedDoi = MetadataMapper.NormalizeDoi(doi);
        if (string.IsNullOrWhiteSpace(normalizedDoi))
        {
            return MetadataMapper.NotFound("arxiv", "DOI is required.");
        }

        var url = $"query?search_query=doi:{Uri.EscapeDataString(normalizedDoi)}&start=0&max_results=1";

        try
        {
            var response = await _httpClient.GetStringAsync(url);
            var papers = ParseAtomFeed(response);
            if (papers.Count == 0)
            {
                return MetadataMapper.NotFound("arxiv", "No ArXiv record found for this DOI.");
            }

            var paper = papers[0];
            paper.PdfUrl = $"https://arxiv.org/pdf/{paper.ExternalId}.pdf";
            return MetadataMapper.FromExternal(paper, "arxiv");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ArXiv DOI lookup failed for {Doi}", normalizedDoi);
            return MetadataMapper.NotFound("arxiv", "Failed to fetch metadata from ArXiv.");
        }
    }

    private static IReadOnlyList<ExternalPaperDto> ParseAtomFeed(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            XNamespace atom = "http://www.w3.org/2005/Atom";
            XNamespace arxiv = "http://arxiv.org/schemas/atom";

            var entries = doc.Descendants(atom + "entry");
            var papers = new List<ExternalPaperDto>();

            foreach (var entry in entries)
            {
                var id = entry.Element(atom + "id")?.Value ?? string.Empty;
                var arxivId = ExtractArxivId(id);
                if (string.IsNullOrWhiteSpace(arxivId)) continue;

                var title = entry.Element(atom + "title")?.Value?.Replace("\n", " ").Trim() ?? "Untitled";
                var summary = entry.Element(atom + "summary")?.Value?.Replace("\n", " ").Trim();
                var publishedStr = entry.Element(atom + "published")?.Value;
                var year = int.TryParse(publishedStr?.Split('-')[0], out var y) ? y : (int?)null;

                var authors = entry.Elements(atom + "author")
                    .Select(a => a.Element(atom + "name")?.Value ?? "Unknown")
                    .ToList();

                var categories = entry.Elements(atom + "category")
                    .Select(c => c.Attribute("term")?.Value)
                    .Where(term => !string.IsNullOrWhiteSpace(term))
                    .Select(term => term!)
                    .ToList();

                var doi = entry.Element(arxiv + "doi")?.Value;
                var url = entry.Element(atom + "id")?.Value;

                papers.Add(new ExternalPaperDto
                {
                    ExternalId = arxivId,
                    Source = "ArXiv",
                    Title = title,
                    Abstract = summary,
                    Year = year,
                    CitationCount = 0,
                    Doi = doi,
                    Url = url,
                    AuthorNames = authors,
                    Keywords = ArxivCategoryMapper.MapCategories(categories).ToList(),
                    Topics = ArxivCategoryMapper.MapCategories(categories).Take(5).ToList(),
                    PdfUrl = $"https://arxiv.org/pdf/{arxivId}.pdf",
                    PdfAccessType = PaperDownloadStatus.AccessTypes.ArXiv,
                    PdfLicense = "arXiv perpetual non-exclusive"
                });
            }

            return papers;
        }
        catch
        {
            return [];
        }
    }

    private static string ExtractArxivId(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        var lastSlash = url.LastIndexOf('/');
        return lastSlash >= 0 ? url[(lastSlash + 1)..] : url;
    }
}
