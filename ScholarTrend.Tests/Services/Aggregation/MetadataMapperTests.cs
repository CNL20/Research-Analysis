using FluentAssertions;
using ScholarTrend.Application.DTOs.Aggregation;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.Services.Aggregation;
using Xunit;

namespace ScholarTrend.Tests.Services.Aggregation;

public class MetadataMapperTests
{
    [Fact]
    public void FromExternal_Maps_Extended_Fields()
    {
        var external = new ExternalPaperDto
        {
            ExternalId = "10.1234/test",
            Source = "Crossref",
            Title = "Test",
            Doi = "10.1234/test",
            Url = "https://doi.org/10.1234/test",
            Journal = "Example Journal",
            PdfUrl = "https://example.com/paper.pdf",
            PdfAccessType = "open_access",
            PdfLicense = "cc-by",
            PublicationType = "journal-article"
        };

        var result = MetadataMapper.FromExternal(external, "crossref");

        result.Url.Should().Be("https://doi.org/10.1234/test");
        result.Journal.Should().Be("Example Journal");
        result.PdfUrl.Should().Be("https://example.com/paper.pdf");
        result.PdfAccessType.Should().Be("open_access");
        result.PdfLicense.Should().Be("cc-by");
        result.PublicationType.Should().Be("journal-article");
    }

    [Fact]
    public void NeedsRefresh_ReturnsTrue_When_Journal_Or_Pdf_Missing()
    {
        MetadataMapper.NeedsRefresh(null).Should().BeTrue();
        MetadataMapper.NeedsRefresh(new PaperSourceMetadataDto { Found = false }).Should().BeTrue();

        MetadataMapper.NeedsRefresh(new PaperSourceMetadataDto
        {
            Found = true,
            Journal = "Nature",
            PdfUrl = "https://example.com/paper.pdf"
        }).Should().BeFalse();

        MetadataMapper.NeedsRefresh(new PaperSourceMetadataDto
        {
            Found = true,
            Journal = null,
            PdfUrl = "https://example.com/paper.pdf"
        }).Should().BeTrue();

        MetadataMapper.NeedsRefresh(new PaperSourceMetadataDto
        {
            Found = true,
            Journal = "Nature",
            PdfUrl = null
        }).Should().BeTrue();
    }
}
