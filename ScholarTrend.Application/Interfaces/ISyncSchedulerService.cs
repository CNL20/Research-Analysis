using ScholarTrend.Application.DTOs.Sync;

namespace ScholarTrend.Application.Interfaces;

public interface ISyncSchedulerService
{
    Task<SyncScheduleDto> GetScheduleConfigAsync();
    Task<SyncScheduleDto> UpdateScheduleConfigAsync(SyncScheduleConfigRequest request);
    Task<ManualSyncResultDto> TriggerManualSyncAsync(string adminUserId, ManualSyncRequest? request = null);
    Task<List<SyncJobInfoDto>> GetJobHistoryAsync(int limit = 50);
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
