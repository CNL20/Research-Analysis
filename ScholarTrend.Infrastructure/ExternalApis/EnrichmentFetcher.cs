using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using ScholarTrend.Application.DTOs.Aggregation;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.External;

namespace ScholarTrend.Infrastructure.ExternalApis;

/// <summary>
/// Rate-limited + retried fetcher for the enrich-job.
/// Each source gets its own semaphore to throttle concurrent requests.
/// </summary>
public class EnrichmentFetcher : IEnrichmentFetcher
{
    private readonly IOpenAlexClient _openAlex;
    private readonly ISemanticScholarClient _semantic;
    private readonly ICrossrefClient _crossref;
    private readonly ILogger<EnrichmentFetcher> _logger;

    private static readonly TimeSpan OpenAlexMinInterval = TimeSpan.FromMilliseconds(1100);
    private static readonly TimeSpan SemanticScholarMinInterval = TimeSpan.FromMilliseconds(3200);
    private static readonly TimeSpan CrossrefMinInterval = TimeSpan.FromMilliseconds(120);

    private readonly SemaphoreSlim _openAlexLock = new(1, 1);
    private readonly SemaphoreSlim _semanticLock = new(1, 1);
    private readonly SemaphoreSlim _crossrefLock = new(1, 1);

    public EnrichmentFetcher(
        IOpenAlexClient openAlex,
        ISemanticScholarClient semantic,
        ICrossrefClient crossref,
        ILogger<EnrichmentFetcher> logger)
    {
        _openAlex = openAlex;
        _semantic = semantic;
        _crossref = crossref;
        _logger = logger;
    }

    public Task<PaperSourceMetadataDto> FetchOpenAlexAsync(string doi, CancellationToken ct = default)
        => ThrottledFetch("openalex", _openAlexLock, OpenAlexMinInterval,
            () => _openAlex.GetByDoiAsync(doi), ct);

    public Task<PaperSourceMetadataDto> FetchSemanticScholarAsync(string doi, CancellationToken ct = default)
        => ThrottledFetch("semantic_scholar", _semanticLock, SemanticScholarMinInterval,
            () => _semantic.GetByDoiAsync(doi), ct);

    public Task<PaperSourceMetadataDto> FetchCrossrefAsync(string doi, CancellationToken ct = default)
        => ThrottledFetch("crossref", _crossrefLock, CrossrefMinInterval,
            () => _crossref.GetByDoiAsync(doi), ct);

    private async Task<PaperSourceMetadataDto> ThrottledFetch(
        string sourceKey,
        SemaphoreSlim gate,
        TimeSpan minInterval,
        Func<Task<PaperSourceMetadataDto>> fetch,
        CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            await Task.Delay(minInterval, ct);

            var policy = BuildRetryPolicy(sourceKey);
            return await policy.ExecuteAsync(async _ => await fetch(), ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private AsyncRetryPolicy<PaperSourceMetadataDto> BuildRetryPolicy(string sourceKey)
    {
        return Policy<PaperSourceMetadataDto>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .OrResult(r => r == null || !r.Found)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timespan, attempt, _) =>
                {
                    _logger.LogWarning(
                        "Retry {Attempt} for {Source} after {Delay}s (Found={Found})",
                        attempt, sourceKey, timespan.TotalSeconds, outcome.Result?.Found ?? false);
                });
    }
}
