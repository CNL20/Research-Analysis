using ScholarTrend.Application.DTOs.Sync;
using ScholarTrend.Application.DTOs.Common;

namespace ScholarTrend.Application.Interfaces;

public interface ISyncSchedulerService
{
    Task<SyncScheduleDto> GetScheduleConfigAsync();
    Task<SyncScheduleDto> UpdateScheduleConfigAsync(SyncScheduleConfigRequest request);
    Task<ManualSyncResultDto> TriggerManualSyncAsync(string adminUserId, ManualSyncRequest? request = null);
    Task<PagedResult<SyncJobInfoDto>> GetJobHistoryAsync(int page = 1, int pageSize = 20);

    /// <summary>
    /// Returns the list of search queries the scheduler currently considers active
    /// (persisted via UpdateScheduleConfigAsync, or falling back to appsettings.json).
    /// Used by Hangfire jobs so each scheduled run covers all configured topics instead
    /// of the single hard-coded default.
    /// </summary>
    Task<List<string>> GetActiveSearchQueriesAsync();
}

public class SyncJobInfoDto
{
    public int Id { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
