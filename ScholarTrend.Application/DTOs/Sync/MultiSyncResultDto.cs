namespace ScholarTrend.Application.DTOs.Sync;

public class MultiSyncResultDto
{
    public List<SyncResultDto> Results { get; set; } = [];
    public string SyncType { get; set; } = "Manual";
    public string TriggeredBy { get; set; } = "system";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public int TotalFetched { get; set; }
    public int TotalQueued { get; set; }
}
