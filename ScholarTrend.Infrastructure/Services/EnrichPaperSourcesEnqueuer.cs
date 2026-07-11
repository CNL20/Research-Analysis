using Hangfire;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Infrastructure.Jobs;

namespace ScholarTrend.Infrastructure.Services;

/// <summary>
/// Schedules a Hangfire background job to enrich a freshly-imported paper.
/// Delayed by 5 seconds to let the transaction settle and to coalesce bursts.
/// </summary>
public class EnrichPaperSourcesEnqueuer : IEnrichPaperSourcesEnqueuer
{
    private static readonly TimeSpan EnrichmentDelay = TimeSpan.FromSeconds(5);

    private readonly IBackgroundJobClient _hangfire;
    private readonly ILogger<EnrichPaperSourcesEnqueuer> _logger;

    public EnrichPaperSourcesEnqueuer(
        IBackgroundJobClient hangfire,
        ILogger<EnrichPaperSourcesEnqueuer> logger)
    {
        _hangfire = hangfire;
        _logger = logger;
    }

    public Task EnqueueEnrichmentAsync(
        int paperId,
        string? doi,
        string primarySource,
        CancellationToken ct = default)
    {
        var jobId = _hangfire.Schedule<EnrichPaperSourcesJob>(
            job => job.EnrichAsync(paperId),
            EnrichmentDelay);

        _logger.LogInformation(
            "Enqueued enrichment job {JobId} for paper {PaperId} (source={Source}, doi={Doi})",
            jobId, paperId, primarySource, doi ?? "<none>");

        return Task.CompletedTask;
    }
}
