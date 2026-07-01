using ScholarTrend.Application.DTOs.Aggregation;
using ScholarTrend.Application.Interfaces.External;

namespace ScholarTrend.Application.Services.Aggregation;

public static class MergedPaperBuilder
{
    public static ExternalPaperDto? MergeFromSources(
        IReadOnlyDictionary<string, PaperSourceMetadataDto> sources,
        string normalizedDoi)
    {
        var openAlex = GetFound(sources, "openalex");
        var semantic = GetFound(sources, "semantic_scholar");
        var crossref = GetFound(sources, "crossref");
        var arxiv = GetFound(sources, "arxiv");

        var primary = openAlex ?? semantic ?? crossref ?? arxiv;
        if (primary == null)
        {
            return null;
        }

        var title = PickBestText(openAlex?.Title, semantic?.Title, crossref?.Title, arxiv?.Title);
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var doi = MetadataMapper.NormalizeDoi(
            openAlex?.Doi ?? semantic?.Doi ?? crossref?.Doi ?? arxiv?.Doi ?? normalizedDoi);

        var externalId = openAlex?.ExternalId
            ?? semantic?.ExternalId
            ?? arxiv?.ExternalId
            ?? crossref?.ExternalId
            ?? doi;

        var source = openAlex != null
            ? "OpenAlex"
            : semantic != null
                ? "SemanticScholar"
                : arxiv != null
                    ? "ArXiv"
                    : "Crossref";

        return new ExternalPaperDto
        {
            ExternalId = externalId,
            Source = source,
            Title = title,
            Abstract = PickBestText(openAlex?.Abstract, semantic?.Abstract, arxiv?.Abstract),
            Year = openAlex?.Year ?? semantic?.Year ?? crossref?.Year ?? arxiv?.Year,
            CitationCount = MaxCitation(openAlex?.CitationCount, semantic?.CitationCount),
            Doi = doi,
            Url = string.IsNullOrWhiteSpace(doi) ? openAlex?.ExternalId : $"https://doi.org/{doi}",
            Journal = PickBestText(crossref?.Journal, openAlex?.Journal, semantic?.Journal),
            AuthorNames = PickAuthors(openAlex, semantic, crossref, arxiv),
            Keywords = MergeKeywords(openAlex, semantic),
            PdfUrl = arxiv?.PdfUrl ?? openAlex?.PdfUrl
        };
    }

    private static PaperSourceMetadataDto? GetFound(
        IReadOnlyDictionary<string, PaperSourceMetadataDto> sources,
        string key)
    {
        return sources.TryGetValue(key, out var value) && value.Found ? value : null;
    }

    private static string? PickBestText(params string?[] values)
    {
        return values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .OrderByDescending(v => v!.Length)
            .FirstOrDefault();
    }

    private static int? MaxCitation(params int?[] values)
    {
        return values.Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty().Max();
    }

    private static List<string> PickAuthors(
        PaperSourceMetadataDto? openAlex,
        PaperSourceMetadataDto? semantic,
        PaperSourceMetadataDto? crossref,
        PaperSourceMetadataDto? arxiv)
    {
        var candidates = new[] { openAlex?.Authors, semantic?.Authors, crossref?.Authors, arxiv?.Authors }
            .Where(a => a is { Count: > 0 })
            .OrderByDescending(a => a!.Count)
            .FirstOrDefault();

        return candidates?
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList() ?? [];
    }

    private static List<string> MergeKeywords(
        PaperSourceMetadataDto? openAlex,
        PaperSourceMetadataDto? semantic)
    {
        return (openAlex?.Keywords ?? [])
            .Concat(semantic?.Keywords ?? [])
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
    }
}
