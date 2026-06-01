namespace ScholarTrend.Domain.Entities;
public class SyncLog
{
    public int Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public int PapersFetched { get; set; }
    public int PapersAdded { get; set; }
    public int PapersUpdated { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}