namespace ScholarTrend.Domain.Entities;

public class MethodPattern
{
    public int Id { get; set; }
    public int TopicId { get; set; }
    public ResearchTopic Topic { get; set; } = null!;
    
    public string MethodName { get; set; } = string.Empty;
    public int PaperCount { get; set; }
    public int Year { get; set; }
    public double GrowthRate { get; set; }
    public DateTime MinedAt { get; set; } = DateTime.UtcNow;
}
