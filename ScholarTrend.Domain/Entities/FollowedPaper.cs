namespace ScholarTrend.Domain.Entities;

public class FollowedPaper
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int PaperId { get; set; }
    public DateTime FollowedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public ResearchPaper Paper { get; set; } = null!;
}
