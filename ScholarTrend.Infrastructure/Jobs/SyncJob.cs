using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.Interfaces;

namespace ScholarTrend.Infrastructure.Jobs;

public class SyncJob : ISyncJob
{
    private const string SyncTypeAutomatic = "Automatic";
    private const string SystemTrigger = "system-scheduler";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SyncJob> _logger;

    public SyncJob(IServiceProvider serviceProvider, ILogger<SyncJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Fetches papers from external APIs and creates a pending sync proposal for admin approval.
    /// Runs automatically based on configured schedule (default: 2:00 AM daily).
    /// </summary>
    public async Task RunAsync()
    {
        _logger.LogInformation("Scheduled (Automatic) sync job started at {Time}", DateTime.UtcNow);

        using var scope = _serviceProvider.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();

        try
        {
            var result = await syncService.RunSyncAsync(sourceName: null, syncType: SyncTypeAutomatic, triggeredBy: SystemTrigger);

            var totalFetched = result.TotalFetched;
            var totalQueued = result.TotalQueued;
            var failedCount = result.Results.Count(r => r.Status == "Failed");
            var skippedCount = result.Results.Count(r => r.Status == "Skipped");

            _logger.LogInformation(
                "Scheduled (Automatic) sync completed: Fetched={Fetched}, Queued={Queued}, Failed={Failed}, Skipped={Skipped}",
                totalFetched, totalQueued, failedCount, skippedCount);

            if (failedCount > 0)
            {
                var failedSources = result.Results
                    .Where(r => r.Status == "Failed")
                    .Select(r => $"{r.Source}: {r.Message}")
                    .ToList();

                _logger.LogWarning("Some sources failed: {FailedSources}",
                    string.Join("; ", failedSources));
            }

            if (skippedCount > 0)
            {
                _logger.LogInformation("{Count} source(s) were skipped because a manual sync was already in progress.", skippedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled (Automatic) sync job failed with exception");
            throw;
        }
    }
}
