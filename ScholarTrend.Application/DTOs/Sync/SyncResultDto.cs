namespace ScholarTrend.Application.DTOs.Sync;

public class SyncResultDto
{
    public int? SyncProposalId { get; set; }
    public int SyncLogId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? Query { get; set; }
    public int PapersFetched { get; set; }
    public int PapersAdded { get; set; }
    public int PapersUpdated { get; set; }
    public int PapersSkippedDuplicates { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}
