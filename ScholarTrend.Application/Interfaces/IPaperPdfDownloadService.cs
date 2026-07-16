namespace ScholarTrend.Application.Interfaces;

/// <summary>
/// Service dùng bởi caller (vd: SyncService.ApprovePendingSyncAsync) để enqueue PDF download.
/// </summary>
public interface IPaperPdfEnqueuer
{
    /// <summary>
    /// Ghi record mới với trạng thái Queued + đẩy vào channel.
    /// </summary>
    Task EnqueueAsync(string externalSource, string sourceUrl, int researchPaperId, CancellationToken ct = default);
}

/// <summary>
/// Service dùng bởi BackgroundService (qua scope) để xử lý một PDF cụ thể.
/// </summary>
public interface IPaperPdfProcessor
{
    /// <summary>
    /// Worker gọi method này. Implement đảm bảo retry với exponential backoff (tối đa 3 lần).
    /// </summary>
    Task ProcessAsync(int paperPdfFileId, CancellationToken ct);
}
