namespace ScholarTrend.Application.DTOs.Sync;

public class SyncResultDto
{
    public int? SyncProposalId { get; set; }
    public int SyncLogId { get; set; }
    public string Source { get; set; } = string.Empty;
    public int PapersFetched { get; set; }
    public int PapersAdded { get; set; }
    public int PapersUpdated { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}
