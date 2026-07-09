using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.Services;
using ScholarTrend.Domain.Constants;
using ScholarTrend.Infrastructure.ExternalApis;

namespace ScholarTrend.Tests.ExternalApis;

/// <summary>
/// Tests cho 4 client API ngoài, focus vào PDF URL mapping.
/// Mỗi client trả về ExternalPaperDto phải có PdfUrl + PdfAccessType đúng.
/// </summary>
public class ExternalClientPdfMappingTests
{
    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExternalApis:SemanticScholar:SearchQuery"] = "ai",
                ["ExternalApis:SemanticScholar:PageSize"] = "10",
                ["ExternalApis:SemanticScholar:BaseUrl"] = "https://api.semanticscholar.org/graph/v1",
                ["ExternalApis:OpenAlex:SearchQuery"] = "ai",
                ["ExternalApis:OpenAlex:PageSize"] = "10",
                ["ExternalApis:OpenAlex:BaseUrl"] = "https://api.openalex.org",
                ["ExternalApis:Crossref:SearchQuery"] = "ai",
                ["ExternalApis:Crossref:BaseUrl"] = "https://api.crossref.org",
                ["ExternalApis:ArXiv:SearchQuery"] = "ai",
                ["ExternalApis:ArXiv:BaseUrl"] = "https://export.arxiv.org/api"
            })
            .Build();

    private static ILogger<T> NullLogger<T>() =>
        Mock.Of<ILogger<T>>();

    // ========================================================================
    // ARXIV CLIENT — đảm bảo PdfUrl = https://arxiv.org/pdf/{id}.pdf
    // ========================================================================

    [Fact]
    public async Task ArXivClient_MapsPdfUrl_FromArxivId()
    {
        const string xml = """
            <?xml version="1.0"?>
            <feed xmlns="http://www.w3.org/2005/Atom"
                  xmlns:arxiv="http://arxiv.org/schemas/atom">
              <entry>
                <id>http://arxiv.org/abs/2401.12345v1</id>
                <title>Test Paper</title>
                <summary>An abstract</summary>
                <published>2024-01-15T00:00:00Z</published>
                <author><name>Alice</name></author>
                <arxiv:doi>10.1234/test</arxiv:doi>
              </entry>
            </feed>
            """;
        var handler = new StubHandler(xml);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://export.arxiv.org/api/") };

        var client = new ArXivClient(http, BuildConfig(), NullLogger<ArXivClient>());
        var result = await client.SearchPapersAsync("test", 5);

        result.Should().HaveCount(1);
        result[0].PdfUrl.Should().Be("https://arxiv.org/pdf/2401.12345v1.pdf");
        result[0].PdfAccessType.Should().Be(PaperDownloadStatus.AccessTypes.ArXiv);
        result[0].PdfLicense.Should().Be("arXiv perpetual non-exclusive");
    }

    [Fact]
    public async Task ArXivClient_EmptyFeed_ReturnsEmpty()
    {
        const string xml = """<?xml version="1.0"?><feed xmlns="http://www.w3.org/2005/Atom"></feed>""";
        var handler = new StubHandler(xml);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://export.arxiv.org/api/") };

        var client = new ArXivClient(http, BuildConfig(), NullLogger<ArXivClient>());
        var result = await client.SearchPapersAsync("nothing", 5);

        result.Should().BeEmpty();
    }

    // ========================================================================
    // CROSSREF CLIENT — PdfUrl từ link có content-type application/pdf
    // ========================================================================

    [Fact]
    public async Task CrossrefClient_MapsPdfUrl_FromLinkArray()
    {
        var json = """
            {
              "message": {
                "items": [
                  {
                    "DOI": "10.1234/test",
                    "title": ["A Test Paper"],
                    "container-title": ["Nature Communications"],
                    "is-referenced-by-count": 42,
                    "published": { "date-parts": [[2024, 1, 15]] },
                    "author": [{ "given": "Alice", "family": "Smith" }],
                    "link": [
                      { "URL": "https://www.nature.com/articles/foo", "content-type": "text/html" },
                      { "URL": "https://www.nature.com/articles/foo.pdf", "content-type": "application/pdf" }
                    ],
                    "license": [{ "URL": "https://creativecommons.org/licenses/by/4.0/" }]
                  }
                ]
              }
            }
            """;
        var handler = new StubHandler(json, "application/json");
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.crossref.org/") };

        var client = new CrossrefClient(http, BuildConfig(), NullLogger<CrossrefClient>());
        var result = await client.SearchPapersAsync("test", 5);

        result.Should().HaveCount(1);
        result[0].PdfUrl.Should().Be("https://www.nature.com/articles/foo.pdf");
        result[0].PdfAccessType.Should().Be(PaperDownloadStatus.AccessTypes.Publisher);
        result[0].PdfLicense.Should().Contain("creativecommons.org");
    }

    [Fact]
    public async Task CrossrefClient_NoPdfLink_PdfUrlIsNull()
    {
        var json = """
            {
              "message": {
                "items": [
                  {
                    "DOI": "10.1234/no-pdf",
                    "title": ["No PDF Paper"],
                    "container-title": ["Some Journal"],
                    "is-referenced-by-count": 5,
                    "published": { "date-parts": [[2023]] },
                    "author": [],
                    "link": [
                      { "URL": "https://example.com/article", "content-type": "text/html" }
                    ]
                  }
                ]
              }
            }
            """;
        var handler = new StubHandler(json, "application/json");
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.crossref.org/") };

        var client = new CrossrefClient(http, BuildConfig(), NullLogger<CrossrefClient>());
        var result = await client.SearchPapersAsync("test", 5);

        result.Should().HaveCount(1);
        result[0].PdfUrl.Should().BeNull();
        result[0].PdfAccessType.Should().BeNull();
    }

    // ========================================================================
    // OPENALEX CLIENT — PdfUrl từ open_access.oa_url; isOa quyết định access type
    // ========================================================================

    [Fact]
    public async Task OpenAlexClient_MapsPdfUrl_WhenIsOaTrue()
    {
        var json = """
            {
              "results": [
                {
                  "id": "https://openalex.org/W123",
                  "display_name": "OA Paper",
                  "publication_year": 2024,
                  "cited_by_count": 10,
                  "doi": "https://doi.org/10.1234/oa",
                  "abstract_inverted_index": { "word": [0] },
                  "open_access": {
                    "is_oa": true,
                    "oa_url": "https://oa-journal.org/full.pdf",
                    "oa_status": "gold"
                  }
                }
              ]
            }
            """;
        var handler = new StubHandler(json, "application/json");
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.openalex.org/") };

        var client = new OpenAlexClient(http, BuildConfig(), NullLogger<OpenAlexClient>());
        var result = await client.SearchPapersAsync("test", 5);

        result.Should().HaveCount(1);
        result[0].PdfUrl.Should().Be("https://oa-journal.org/full.pdf");
        result[0].PdfAccessType.Should().Be(PaperDownloadStatus.AccessTypes.OpenAccess);
    }

    [Fact]
    public async Task OpenAlexClient_PdfUrlExistsButIsOaFalse_MarksAsClosed()
    {
        var json = """
            {
              "results": [
                {
                  "id": "https://openalex.org/W456",
                  "display_name": "Closed Paper",
                  "publication_year": 2024,
                  "cited_by_count": 0,
                  "doi": null,
                  "open_access": {
                    "is_oa": false,
                    "oa_url": "https://paywall.com/x.pdf",
                    "oa_status": "closed"
                  }
                }
              ]
            }
            """;
        var handler = new StubHandler(json, "application/json");
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.openalex.org/") };

        var client = new OpenAlexClient(http, BuildConfig(), NullLogger<OpenAlexClient>());
        var result = await client.SearchPapersAsync("test", 5);

        result.Should().HaveCount(1);
        result[0].PdfUrl.Should().Be("https://paywall.com/x.pdf");
        result[0].PdfAccessType.Should().Be(PaperDownloadStatus.AccessTypes.Closed,
            "is_oa=false means we must NOT trigger download");
    }

    // ========================================================================
    // SEMANTICSCHOLAR — PdfUrl từ openAccessPdf.url
    // ========================================================================

    [Fact]
    public async Task SemanticScholarClient_MapsPdfUrl_FromOpenAccessPdf()
    {
        var json = """
            {
              "data": [
                {
                  "paperId": "abc123",
                  "title": "SS Paper",
                  "abstract": "An abstract",
                  "year": 2024,
                  "citationCount": 7,
                  "url": "https://semanticscholar.org/paper/abc",
                  "externalIds": { "DOI": "10.1234/ss" },
                  "authors": [{ "name": "Alice" }],
                  "openAccessPdf": { "url": "https://arxiv.org/pdf/abc.pdf" }
                }
              ]
            }
            """;
        var handler = new StubHandler(json, "application/json");
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.semanticscholar.org/graph/v1/") };

        var client = new SemanticScholarClient(http, BuildConfig(), NullLogger<SemanticScholarClient>());
        var result = await client.SearchPapersAsync("test", 5);

        result.Should().HaveCount(1);
        result[0].PdfUrl.Should().Be("https://arxiv.org/pdf/abc.pdf");
        result[0].PdfAccessType.Should().Be(PaperDownloadStatus.AccessTypes.OpenAccess);
    }

    [Fact]
    public async Task SemanticScholarClient_NoOpenAccessPdf_PdfUrlIsNull()
    {
        var json = """
            {
              "data": [
                {
                  "paperId": "no-pdf-id",
                  "title": "No PDF",
                  "year": 2023,
                  "authors": []
                }
              ]
            }
            """;
        var handler = new StubHandler(json, "application/json");
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.semanticscholar.org/graph/v1/") };

        var client = new SemanticScholarClient(http, BuildConfig(), NullLogger<SemanticScholarClient>());
        var result = await client.SearchPapersAsync("test", 5);

        result.Should().HaveCount(1);
        result[0].PdfUrl.Should().BeNull();
        result[0].PdfAccessType.Should().BeNull();
    }
}

/// <summary>
/// HttpMessageHandler stub trả về response với body cố định cho mọi request.
/// Dùng để test HTTP client mà không cần server thật.
/// </summary>
internal class StubHandler : HttpMessageHandler
{
    private readonly string _responseBody;
    private readonly string _contentType;

    public StubHandler(string responseBody, string contentType = "text/xml")
    {
        _responseBody = responseBody;
        _contentType = contentType;
    }

    public List<HttpRequestMessage> CapturedRequests { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CapturedRequests.Add(request);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responseBody, System.Text.Encoding.UTF8, _contentType)
        };
    }
}