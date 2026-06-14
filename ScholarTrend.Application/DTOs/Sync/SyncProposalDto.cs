namespace ScholarTrend.Application.DTOs.Sync;

public class SyncProposalDto
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalFetched { get; set; }
    public int TotalApproved { get; set; }
    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public List<PendingPaperDto> Papers { get; set; } = [];
}
