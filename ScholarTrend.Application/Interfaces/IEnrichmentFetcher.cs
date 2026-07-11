using ScholarTrend.Application.DTOs.Aggregation;

namespace ScholarTrend.Application.Interfaces;

/// <summary>
/// Fetches metadata from a single source by DOI with built-in rate-limiting and retry.
/// Implementations must respect the source's rate-limit (e.g. OpenAlex 1 req/sec,
/// SemanticScholar 1 req/3s, Crossref ~50 req/sec).
/// </summary>
public interface IEnrichmentFetcher
{
    Task<PaperSourceMetadataDto> FetchOpenAlexAsync(string doi, CancellationToken ct = default);
    Task<PaperSourceMetadataDto> FetchSemanticScholarAsync(string doi, CancellationToken ct = default);
    Task<PaperSourceMetadataDto> FetchCrossrefAsync(string doi, CancellationToken ct = default);
}
