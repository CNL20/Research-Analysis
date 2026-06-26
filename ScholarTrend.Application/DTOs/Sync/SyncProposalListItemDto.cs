namespace ScholarTrend.Application.DTOs.Sync;

public class SyncProposalListItemDto
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalFetched { get; set; }
    public int PendingCount { get; set; }
    public int TotalApproved { get; set; }
}
