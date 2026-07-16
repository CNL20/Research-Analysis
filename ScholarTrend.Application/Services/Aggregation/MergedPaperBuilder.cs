using ScholarTrend.Application.DTOs.Aggregation;
using ScholarTrend.Application.Interfaces.External;

namespace ScholarTrend.Application.Services.Aggregation;

/// <summary>
/// Merges metadata from multiple bibliographic sources into a single unified
/// <see cref="ExternalPaperDto"/>.
///
/// Field priority is: Crossref > OpenAlex > SemanticScholar > ArXiv.
/// - For text fields: pick the first non-empty value in priority order (NOT the longest).
/// - For CitationCount: take the MAX across all sources (more accurate than single-source).
/// - For PdfUrl: prefer ArXiv > OpenAlex > SemanticScholar (ArXiv PDF URLs are most reliable).
/// </summary>
public static class MergedPaperBuilder
{
    public static ExternalPaperDto? MergeFromSources(
        IReadOnlyDictionary<string, PaperSourceMetadataDto> sources,
        string normalizedDoi)
    {
        var crossref = GetFound(sources, "crossref");
        var openAlex = GetFound(sources, "openalex");
        var semantic = GetFound(sources, "semantic_scholar");
        var arxiv = GetFound(sources, "arxiv");

        if (crossref == null && openAlex == null && semantic == null && arxiv == null)
        {
            return null;
        }

        var title = PickByPriority(crossref?.Title, openAlex?.Title, semantic?.Title, arxiv?.Title);
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var abstractText = PickByPriority(
            crossref?.Abstract, openAlex?.Abstract, semantic?.Abstract, arxiv?.Abstract);

        var journal = PickByPriority(crossref?.Journal, openAlex?.Journal, semantic?.Journal);

        var year = PickByPriority(
            crossref?.Year, openAlex?.Year, semantic?.Year, arxiv?.Year);

        var doi = MetadataMapper.NormalizeDoi(
            crossref?.Doi ?? openAlex?.Doi ?? semantic?.Doi ?? arxiv?.Doi ?? normalizedDoi);

        var citationCount = MaxCitation(
            crossref?.CitationCount, openAlex?.CitationCount, semantic?.CitationCount);

        var pdfUrl = arxiv?.PdfUrl ?? openAlex?.PdfUrl ?? semantic?.PdfUrl ?? crossref?.PdfUrl;

        var url = PickByPriority(crossref?.Url, openAlex?.Url, semantic?.Url, arxiv?.Url)
            ?? (!string.IsNullOrWhiteSpace(doi) ? $"https://doi.org/{doi}" : null);

        var publicationType = PickByPriority(
            crossref?.PublicationType, openAlex?.PublicationType, semantic?.PublicationType);

        var pdfAccessType = PickByPriority(
            arxiv?.PdfAccessType, openAlex?.PdfAccessType, semantic?.PdfAccessType, crossref?.PdfAccessType);

        var pdfLicense = PickByPriority(
            crossref?.PdfLicense, openAlex?.PdfLicense, semantic?.PdfLicense, arxiv?.PdfLicense);

        var sourceName = crossref != null
            ? "Crossref"
            : openAlex != null
                ? "OpenAlex"
                : semantic != null
                    ? "SemanticScholar"
                    : "ArXiv";

        var externalId = crossref?.ExternalId
                       ?? openAlex?.ExternalId
                       ?? semantic?.ExternalId
                       ?? arxiv?.ExternalId
                       ?? doi;

        return new ExternalPaperDto
        {
            ExternalId = externalId,
            Source = sourceName,
            Title = title!,
            Abstract = abstractText,
            Year = year,
            CitationCount = citationCount,
            Doi = doi,
            Url = url,
            Journal = journal,
            AuthorNames = PickAuthorsByPriority(crossref, openAlex, semantic, arxiv),
            Keywords = MergeKeywordsByPriority(crossref, openAlex, semantic),
            PdfUrl = pdfUrl,
            PdfAccessType = pdfAccessType,
            PdfLicense = pdfLicense,
            PublicationType = publicationType
        };
    }

    private static PaperSourceMetadataDto? GetFound(
        IReadOnlyDictionary<string, PaperSourceMetadataDto> sources, string key)
    {
        return sources.TryGetValue(key, out var value) && value.Found ? value : null;
    }

    /// <summary>Q7: pick the first non-null value in priority order (not the longest).</summary>
    private static string? PickByPriority(params string?[] values)
    {
        return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }

    private static int? PickByPriority(params int?[] values)
    {
        return values.FirstOrDefault(v => v.HasValue);
    }

    private static int? MaxCitation(params int?[] values)
    {
        var filtered = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return filtered.Count == 0 ? null : filtered.Max();
    }

    private static List<string> PickAuthorsByPriority(
        PaperSourceMetadataDto? crossref,
        PaperSourceMetadataDto? openAlex,
        PaperSourceMetadataDto? semantic,
        PaperSourceMetadataDto? arxiv)
    {
        var candidates = new[] { crossref, openAlex, semantic, arxiv }
            .Where(s => s?.Authors is { Count: > 0 })
            .Select(s => s!.Authors)
            .FirstOrDefault();

        return candidates?
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList() ?? [];
    }

    private static List<string> MergeKeywordsByPriority(
        PaperSourceMetadataDto? crossref,
        PaperSourceMetadataDto? openAlex,
        PaperSourceMetadataDto? semantic)
    {
        return (crossref?.Keywords ?? [])
            .Concat(openAlex?.Keywords ?? [])
            .Concat(semantic?.Keywords ?? [])
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
    }
}
