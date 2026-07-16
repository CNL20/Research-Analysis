namespace ScholarTrend.Application.Services.Aggregation;

public static class SourceNameNormalizer
{
    public static string ToMergeKey(string sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return string.Empty;
        }

        return sourceName.Trim().ToLowerInvariant() switch
        {
            "crossref" => "crossref",
            "openalex" => "openalex",
            "semanticscholar" or "semantic_scholar" => "semantic_scholar",
            "arxiv" => "arxiv",
            _ => sourceName.Trim().ToLowerInvariant()
        };
    }

    public static string ToStorageName(string mergeKey) => mergeKey.ToLowerInvariant() switch
    {
        "crossref" => "Crossref",
        "openalex" => "OpenAlex",
        "semantic_scholar" => "SemanticScholar",
        "arxiv" => "ArXiv",
        _ => mergeKey
    };
}
