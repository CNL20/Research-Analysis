namespace ScholarTrend.Domain.Entities;

public class SyncProposal
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = Constants.SyncProposalStatus.Pending;
    public int TotalFetched { get; set; }
    public int TotalApproved { get; set; }
    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public ICollection<PendingPaper> PendingPapers { get; set; } = [];
}
