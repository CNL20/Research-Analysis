namespace ScholarTrend.Domain.Entities;
public class FollowedJournal
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int JournalId { get; set; }
    public DateTime FollowedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public Journal Journal { get; set; } = null!;
}