namespace ScholarTrend.Application.Interfaces;

/// <summary>
/// Rebuilds KeywordTrends / TopicTrends / JournalTrends for all browsable paper
/// publication months (capped), not only the last 12 months.
/// </summary>
public interface ITrendAggregationService
{
    /// <summary>
    /// Full rebuild for the rolling 12-month window (upsert + prune).
    /// </summary>
    Task RebuildAsync(CancellationToken ct = default);

    /// <summary>
    /// Rebuild only when trend tables are empty or not aligned with the current rolling window.
    /// </summary>
    Task EnsureBuiltAsync(CancellationToken ct = default);

    /// <summary>
    /// Schedule a debounced Hangfire rebuild (60s). Consecutive calls reset the timer
    /// so a burst of approvals shares one job. Non-blocking.
    /// </summary>
    void ScheduleRebuild();

    /// <summary>
    /// Enqueue ensure-built on Hangfire (non-blocking).
    /// </summary>
    void ScheduleEnsureBuilt();
}
