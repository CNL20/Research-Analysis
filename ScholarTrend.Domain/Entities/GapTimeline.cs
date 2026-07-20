namespace ScholarTrend.Domain.Entities;

public class GapTimeline
{
    public int Id { get; set; }
    public int TopicId { get; set; }
    public ResearchTopic Topic { get; set; } = null!;
    
    public int Year { get; set; }
    public string GapType { get; set; } = string.Empty;
    public string GapTitle { get; set; } = string.Empty;
    public int PaperCount { get; set; }
    public bool IsResolved { get; set; }
    public int? ResolvedInYear { get; set; }
    public string Trend { get; set; } = GapTrends.Stable;
    public double GrowthRate { get; set; }
    public DateTime TrackedAt { get; set; } = DateTime.UtcNow;
}

public static class GapTrends
{
    public const string Increasing = "increasing";
    public const string Decreasing = "decreasing";
    public const string Stable = "stable";
    public const string Emerging = "emerging";
}
