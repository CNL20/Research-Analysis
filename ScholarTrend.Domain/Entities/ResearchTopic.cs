namespace ScholarTrend.Domain.Entities;

public class ResearchTopic
{
    public int Id { get; set; }
    public string TopicName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<PaperTopic> PaperTopics { get; set; } = [];
    public ICollection<FollowedTopic> FollowedTopics { get; set; } = [];
    public ICollection<TopicTrend> TopicTrends { get; set; } = [];
}