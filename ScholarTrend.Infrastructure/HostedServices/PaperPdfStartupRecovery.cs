using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Constants;

namespace ScholarTrend.Infrastructure.HostedServices;

/// <summary>
/// Khi app start, tìm các record PaperPdfFile đang kẹt ở trạng thái Queued/Downloading
/// (do tắt server giữa chừng) rồi đẩy lại vào channel xử lý.
/// </summary>
public class PaperPdfStartupRecovery : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaperPdfStartupRecovery> _logger;

    public PaperPdfStartupRecovery(
        IServiceScopeFactory scopeFactory,
        ILogger<PaperPdfStartupRecovery> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPaperPdfFileRepository>();
            var enqueuer = scope.ServiceProvider.GetRequiredService<IPaperPdfEnqueuer>();

            var stuck = await repo.GetStuckAsync(new[]
            {
                PaperDownloadStatus.Queued,
                PaperDownloadStatus.Downloading
            });

            if (stuck.Count == 0)
            {
                _logger.LogInformation("PDF startup recovery: no stuck records found.");
                return;
            }

            _logger.LogWarning(
                "PDF startup recovery: re-queueing {Count} stuck PDF(s)",
                stuck.Count);

            foreach (var item in stuck)
            {
                item.Status = PaperDownloadStatus.Queued;
                item.AttemptCount = 0;  // reset retry counter
                repo.Update(item);
            }

            await repo.SaveChangesAsync();

            foreach (var item in stuck)
            {
                await enqueuer.EnqueueAsync(
                    externalSource: item.ExternalSource,
                    sourceUrl: item.SourceUrl,
                    researchPaperId: item.ResearchPaperId,
                    ct: ct);

                _logger.LogInformation(
                    "Re-queued PDF download for paper {PaperId}",
                    item.ResearchPaperId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF startup recovery failed");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
