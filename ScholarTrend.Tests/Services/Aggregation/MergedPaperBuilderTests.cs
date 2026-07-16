using FluentAssertions;
using ScholarTrend.Application.DTOs.Aggregation;
using ScholarTrend.Application.Services.Aggregation;
using Xunit;

namespace ScholarTrend.Tests.Services.Aggregation;

public class MergedPaperBuilderTests
{
    private static PaperSourceMetadataDto Crossref(string title = "Title from Crossref") => new()
    {
        Source = "crossref",
        Found = true,
        Title = title,
        Doi = "10.1234/test",
        Year = 2024,
        Journal = "Crossref Journal",
        Abstract = "Crossref abstract",
        CitationCount = 100,
        ExternalId = "CR-1"
    };

    private static PaperSourceMetadataDto OpenAlex(string title = "Title from OpenAlex") => new()
    {
        Source = "openalex",
        Found = true,
        Title = title,
        Doi = "10.1234/test",
        Year = 2024,
        Abstract = "OpenAlex abstract",
        CitationCount = 250,
        ExternalId = "OA-1"
    };

    private static PaperSourceMetadataDto SemanticScholar() => new()
    {
        Source = "semantic_scholar",
        Found = true,
        Title = "Title from SemanticScholar",
        Doi = "10.1234/test",
        Year = 2024,
        Abstract = "Semantic abstract",
        CitationCount = 50,
        ExternalId = "SS-1"
    };

    private static PaperSourceMetadataDto Arxiv() => new()
    {
        Source = "arxiv",
        Found = true,
        Title = "Title from ArXiv",
        Doi = "10.1234/test",
        Year = 2024,
        Abstract = "Arxiv abstract",
        PdfUrl = "https://arxiv.org/pdf/1234.pdf",
        ExternalId = "2506.12345"
    };

    [Fact]
    public void MergeFromSources_Title_Prioritizes_Crossref_Over_OpenAlex()
    {
        // Q7: priority, NOT length
        var sources = new Dictionary<string, PaperSourceMetadataDto>
        {
            ["openalex"] = OpenAlex("Title from OpenAlex (this is longer)"),
            ["crossref"] = Crossref("Short CR title")
        };

        var result = MergedPaperBuilder.MergeFromSources(sources, "");

        result.Should().NotBeNull();
        result!.Title.Should().Be("Short CR title");
    }

    [Fact]
    public void MergeFromSources_Title_Priority_Order_Crossref_OpenAlex_Semantic_Arxiv()
    {
        var sources = new Dictionary<string, PaperSourceMetadataDto>
        {
            ["arxiv"] = Arxiv(),
            ["semantic_scholar"] = SemanticScholar(),
            ["openalex"] = OpenAlex(),
            ["crossref"] = Crossref()
        };

        var result = MergedPaperBuilder.MergeFromSources(sources, "");

        result!.Title.Should().Be("Title from Crossref");
    }

    [Fact]
    public void MergeFromSources_Title_Falls_Back_To_Next_Priority_When_Crossref_Missing()
    {
        var sources = new Dictionary<string, PaperSourceMetadataDto>
        {
            ["arxiv"] = Arxiv(),
            ["semantic_scholar"] = SemanticScholar(),
            ["openalex"] = OpenAlex()
        };

        var result = MergedPaperBuilder.MergeFromSources(sources, "");

        result!.Title.Should().Be("Title from OpenAlex");
    }

    [Fact]
    public void MergeFromSources_CitationCount_Takes_Max_Across_Sources()
    {
        var sources = new Dictionary<string, PaperSourceMetadataDto>
        {
            ["openalex"] = OpenAlex(),
            ["semantic_scholar"] = SemanticScholar(),
            ["crossref"] = Crossref()
        };

        var result = MergedPaperBuilder.MergeFromSources(sources, "");

        result!.CitationCount.Should().Be(250);
    }

    [Fact]
    public void MergeFromSources_PdfUrl_Prefers_ArXiv()
    {
        var sources = new Dictionary<string, PaperSourceMetadataDto>
        {
            ["openalex"] = OpenAlex(),
            ["arxiv"] = Arxiv()
        };

        var result = MergedPaperBuilder.MergeFromSources(sources, "");

        result!.PdfUrl.Should().Be("https://arxiv.org/pdf/1234.pdf");
    }

    [Fact]
    public void MergeFromSources_Doi_Normalizes_To_Lowercase_No_Prefix()
    {
        var sources = new Dictionary<string, PaperSourceMetadataDto>
        {
            ["crossref"] = new() { Found = true, Doi = "HTTPS://DOI.ORG/10.1234/Test", Title = "t" }
        };

        var result = MergedPaperBuilder.MergeFromSources(sources, "");

        result!.Doi.Should().Be("10.1234/test");
    }

    [Fact]
    public void MergeFromSources_Url_Uses_Doi_When_Present()
    {
        var sources = new Dictionary<string, PaperSourceMetadataDto>
        {
            ["crossref"] = new()
            {
                Found = true,
                Title = "t",
                Doi = "10.1234/test"
            }
        };

        var result = MergedPaperBuilder.MergeFromSources(sources, "");

        result!.Url.Should().Be("https://doi.org/10.1234/test");
    }

    [Fact]
    public void MergeFromSources_Returns_Null_When_All_Sources_Missing()
    {
        var sources = new Dictionary<string, PaperSourceMetadataDto>
        {
            ["openalex"] = new() { Found = false }
        };

        var result = MergedPaperBuilder.MergeFromSources(sources, "");

        result.Should().BeNull();
    }

    [Fact]
    public void MergeFromSources_Returns_Null_When_Title_Empty_Across_All_Sources()
    {
        var sources = new Dictionary<string, PaperSourceMetadataDto>
        {
            ["crossref"] = new() { Found = true, Title = "" },
            ["openalex"] = new() { Found = true, Title = "" }
        };

        var result = MergedPaperBuilder.MergeFromSources(sources, "");

        result.Should().BeNull();
    }

    [Fact]
    public void MergeFromSources_Authors_Picks_From_First_NonEmpty_Priority_Source()
    {
        var sources = new Dictionary<string, PaperSourceMetadataDto>
        {
            ["openalex"] = OpenAlex(), // authors null
            ["semantic_scholar"] = new()
            {
                Found = true,
                Title = "x",
                Authors = new List<string> { "Alice", "Bob" }
            }
        };

        var result = MergedPaperBuilder.MergeFromSources(sources, "");

        result!.AuthorNames.Should().BeEquivalentTo(new[] { "Alice", "Bob" });
    }

    [Fact]
    public void MergeFromSources_Source_Name_Reflects_Highest_Priority_Found_Source()
    {
        var sources = new Dictionary<string, PaperSourceMetadataDto>
        {
            ["arxiv"] = Arxiv(),
            ["openalex"] = OpenAlex()
        };

        var result = MergedPaperBuilder.MergeFromSources(sources, "");

        result!.Source.Should().Be("OpenAlex");
    }

    [Fact]
    public void MergeFromSources_Only_ArXiv_Available_Sets_Source_To_ArXiv()
    {
        var sources = new Dictionary<string, PaperSourceMetadataDto>
        {
            ["arxiv"] = Arxiv()
        };

        var result = MergedPaperBuilder.MergeFromSources(sources, "");

        result!.Source.Should().Be("ArXiv");
    }
}