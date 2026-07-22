using ScholarTrend.Application.Interfaces;

namespace ScholarTrend.Infrastructure.Jobs;

/// <summary>
/// Hangfire entry point for rebuilding Keyword / Topic / Journal trends.
/// </summary>
public class RecalculateTrendsJob
{
    private readonly ITrendAggregationService _trendAggregation;

    public RecalculateTrendsJob(ITrendAggregationService trendAggregation)
    {
        _trendAggregation = trendAggregation;
    }

    public Task RunAsync(CancellationToken ct = default)
        => _trendAggregation.RebuildAsync(ct);

    public Task EnsureBuiltAsync(CancellationToken ct = default)
        => _trendAggregation.EnsureBuiltAsync(ct);
}
