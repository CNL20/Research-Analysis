using Microsoft.Extensions.DependencyInjection;
using ScholarTrend.Application.Interfaces;

namespace ScholarTrend.Infrastructure.Jobs;

public class SyncJob
{
    private readonly IServiceProvider _serviceProvider;

    public SyncJob(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Fetches papers from external APIs and creates a pending sync proposal for admin approval.
    /// </summary>
    public async Task RunAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();
        await syncService.RunSyncAsync();
    }
}
