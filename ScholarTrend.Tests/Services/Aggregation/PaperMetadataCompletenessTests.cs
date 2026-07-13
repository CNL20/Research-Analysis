using FluentAssertions;
using ScholarTrend.Application.Services.Aggregation;
using ScholarTrend.Domain.Entities;
using Xunit;

namespace ScholarTrend.Tests.Services.Aggregation;

public class PaperMetadataCompletenessTests
{
    [Fact]
    public void GetMissingFields_Returns_All_Gaps()
    {
        var paper = new ResearchPaper
        {
            Title = "Test",
            Doi = null,
            Abstract = null,
            PdfUrl = null,
            JournalId = null,
            PaperAuthors = [],
            PaperKeywords = []
        };

        var missing = PaperMetadataCompleteness.GetMissingFields(paper);

        missing.Should().Contain(["doi", "abstract", "journal", "authors", "keywords", "pdfUrl"]);
    }

    [Fact]
    public void GetMissingFields_Returns_Empty_When_Complete()
    {
        var paper = new ResearchPaper
        {
            Title = "Test",
            Doi = "10.1234/test",
            Abstract = "Abstract",
            PdfUrl = "https://example.com/paper.pdf",
            JournalId = 1,
            PaperAuthors = [new PaperAuthor { Author = new Author { Name = "Alice" } }],
            PaperKeywords = [new PaperKeyword { Keyword = new Keyword { Name = "AI" } }]
        };

        PaperMetadataCompleteness.GetMissingFields(paper).Should().BeEmpty();
    }
}
