namespace ScholarTrend.Application.Interfaces;

/// <summary>
/// Resolves an ArXiv identifier (e.g. "2506.12345" or "http://arxiv.org/abs/2506.12345v1")
/// to a canonical DOI via OpenAlex.
/// Returns null when the paper has no DOI registered.
/// Results are cached in-memory for 7 days to avoid hammering the API.
/// </summary>
public interface IArxivDoiResolver
{
    Task<string?> ResolveDoiAsync(string arxivId, CancellationToken ct = default);
}
