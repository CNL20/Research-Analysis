namespace ScholarTrend.Domain.Entities;
public class FollowedTopic
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int TopicId { get; set; }
    public DateTime FollowedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public ResearchTopic Topic { get; set; } = null!;
}
