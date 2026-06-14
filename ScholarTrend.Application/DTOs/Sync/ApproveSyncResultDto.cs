namespace ScholarTrend.Application.DTOs.Sync;

public class ApproveSyncResultDto
{
    public int SyncProposalId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int PapersApproved { get; set; }
    public int PapersRejected { get; set; }
    public string Message { get; set; } = string.Empty;
}
