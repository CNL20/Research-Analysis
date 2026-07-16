using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.Interfaces;

namespace ScholarTrend.Infrastructure.HostedServices;

/// <summary>
/// BackgroundService worker: đọc IPaperPdfChannel.Reader (Singleton) + tạo scope
/// để gọi IPaperPdfProcessor.ProcessAsync (Scoped). Pattern bắt buộc vì:
///   - HostedService là Singleton → KHÔNG thể inject Scoped service
///   - IPaperPdfChannel là Singleton → an toàn để đọc từ host
///   - IPaperPdfProcessor là Scoped → resolve qua scope cho mỗi task
/// Cho phép 3 task chạy song song.
/// </summary>
public class PaperPdfDownloadWorker : BackgroundService
{
    private const int MaxConcurrency = 3;

    private readonly IPaperPdfChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaperPdfDownloadWorker> _logger;
    private readonly SemaphoreSlim _throttle = new(MaxConcurrency, MaxConcurrency);

    public PaperPdfDownloadWorker(
        IPaperPdfChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<PaperPdfDownloadWorker> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation(
            "PaperPdfDownloadWorker started (concurrency={Concurrency})",
            MaxConcurrency);

        await foreach (var id in _channel.Reader.ReadAllAsync(ct))
        {
            await _throttle.WaitAsync(ct);
            _ = Task.Run(async () =>
            {
                try
                {
                    // Tạo scope mới cho mỗi task — đảm bảo IPaperPdfProcessor (Scoped) có lifetime đúng
                    using var scope = _scopeFactory.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<IPaperPdfProcessor>();
                    await processor.ProcessAsync(id, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Worker crashed for PDF #{Id}", id);
                }
                finally
                {
                    _throttle.Release();
                }
            }, ct);
        }
    }
}
