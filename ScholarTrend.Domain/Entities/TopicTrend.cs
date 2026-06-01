namespace ScholarTrend.Domain.Entities;
public class TopicTrend
{
    public int Id { get; set; }
    public int TopicId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int PaperCount { get; set; }
    public int CitationCount { get; set; }
    public double GrowthRate { get; set; }
    public double TrendingScore { get; set; }
    public ResearchTopic Topic { get; set; } = null!;
}