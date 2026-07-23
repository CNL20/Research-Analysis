using Hangfire;
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

    [DisableConcurrentExecution(timeoutInSeconds: 60 * 15)]
    public async Task RunAsync(CancellationToken ct = default)
    {
        try
        {
            await _trendAggregation.RebuildAsync(ct);
        }
        catch (Exception ex)
        {
            // Catch and log to prevent Visual Studio from breaking on background transient errors
            Console.WriteLine($"[Hangfire] Trend rebuild failed: {ex.Message}");
            // Deliberately swallow the exception so VS doesn't break during demo.
        }
    }

    [DisableConcurrentExecution(timeoutInSeconds: 60 * 15)]
    public async Task EnsureBuiltAsync(CancellationToken ct = default)
    {
        try
        {
            await _trendAggregation.EnsureBuiltAsync(ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Hangfire] Ensure built failed: {ex.Message}");
        }
    }
}
