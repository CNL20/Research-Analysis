using ScholarTrend.Application.DTOs.Aggregation;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Services.Aggregation;

public static class MetadataMapper
{
    public static string NormalizeDoi(string? doi)
    {
        if (string.IsNullOrWhiteSpace(doi))
        {
            return string.Empty;
        }

        var value = doi.Trim();
        const string prefix = "https://doi.org/";
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = value[prefix.Length..];
        }

        return value.Trim().ToLowerInvariant();
    }

    public static PaperSourceMetadataDto FromExternal(ExternalPaperDto external, string sourceKey)
    {
        var arxivId = sourceKey == "arxiv" ? external.ExternalId : null;
        var pdfUrl = external.PdfUrl
            ?? (sourceKey == "arxiv" && !string.IsNullOrWhiteSpace(external.ExternalId)
                ? $"https://arxiv.org/pdf/{external.ExternalId}.pdf"
                : null);

        return new PaperSourceMetadataDto
        {
            Source = sourceKey,
            Found = true,
            ExternalId = external.ExternalId,
            Doi = NormalizeDoi(external.Doi),
            Title = external.Title,
            Year = external.Year,
            Journal = external.Journal,
            Authors = external.AuthorNames,
            Abstract = external.Abstract,
            CitationCount = external.CitationCount,
            Keywords = external.Keywords,
            PdfUrl = pdfUrl,
            ArxivId = arxivId,
        };
    }

    public static PaperSourceMetadataDto FromInternalPaper(ResearchPaper paper)
    {
        return new PaperSourceMetadataDto
        {
            Source = "internal",
            Found = true,
            ExternalId = paper.Id.ToString(),
            Doi = NormalizeDoi(paper.Doi),
            Title = paper.Title,
            Year = paper.PublicationYear,
            Journal = paper.Journal?.Name,
            Authors = paper.PaperAuthors
                .OrderBy(pa => pa.AuthorOrder)
                .Select(pa => pa.Author.Name)
                .ToList(),
            Abstract = paper.Abstract,
            CitationCount = paper.CitationCount,
            Keywords = paper.PaperKeywords.Select(pk => pk.Keyword.Name).ToList(),
            PdfUrl = paper.PdfUrl,
        };
    }

    public static PaperSourceMetadataDto NotFound(string source, string message)
    {
        return new PaperSourceMetadataDto
        {
            Source = source,
            Found = false,
            ErrorMessage = message,
        };
    }
}
