namespace ScholarTrend.Application.DTOs.Sync;

public class SyncScheduleDto
{
    public bool Enabled { get; set; }
    public string CronExpression { get; set; } = "0 1 * * *";
    public string TimeZone { get; set; } = "SE Asia Standard Time";
    public List<string> SearchQueries { get; set; } = [];
    public DateTime? LastSyncAt { get; set; }
    public DateTime? NextSyncAt { get; set; }
}

public class SyncScheduleConfigRequest
{
    public bool Enabled { get; set; } = true;
    public string CronExpression { get; set; } = "0 1 * * *";
    public string TimeZone { get; set; } = "SE Asia Standard Time";
    public List<string> SearchQueries { get; set; } = [];
}

public class ManualSyncRequest
{
    public string? SourceName { get; set; }
    public int PaperLimit { get; set; } = 10;
    public string? SearchQuery { get; set; }
}

public class ManualSyncResultDto
{
    public bool Success { get; set; }
    public string SyncType { get; set; } = "Manual";
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    public string TriggeredBy { get; set; } = string.Empty;
    public string? SourceName { get; set; }
    public int PapersFetched { get; set; }
    public int PapersQueued { get; set; }
    public int ProposalId { get; set; }
    public List<SourceSyncResultDto> SourceResults { get; set; } = [];
    public string? Message { get; set; }
}

public class SourceSyncResultDto
{
    public string SourceName { get; set; } = string.Empty;
    public int PapersFetched { get; set; }
    public int PapersQueued { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? Message { get; set; }
}

public class SyncLockStatusDto
{
    public string SourceName { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public string? SyncType { get; set; }
    public string? TriggeredBy { get; set; }
    public DateTime? LockedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class SyncStatusOverviewDto
{
    public bool IsAnySyncRunning { get; set; }
    public List<SyncLockStatusDto> Sources { get; set; } = [];
    public List<SyncLogDto> RecentSyncs { get; set; } = [];
}
