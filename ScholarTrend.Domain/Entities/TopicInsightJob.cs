namespace ScholarTrend.Domain.Entities;

public class TopicInsightJob
{
    public int Id { get; set; }
    public int TopicId { get; set; }
    public ResearchTopic Topic { get; set; } = null!;
    
    public string Status { get; set; } = string.Empty; // Pending | Extracting | Aggregating | Completed | Failed
    public int PapersProcessed { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
