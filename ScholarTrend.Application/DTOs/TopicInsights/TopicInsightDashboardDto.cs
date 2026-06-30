namespace ScholarTrend.Application.DTOs.TopicInsights;

public class TopicInsightDashboardDto
{
    public int TopicId { get; set; }
    public string TopicName { get; set; } = string.Empty;
    
    // We can merge with TopicTrends data in real implementation. For mock, we just provide timeline and opportunities.
    public List<TimelineDto> Timeline { get; set; } = [];
    public List<ResearchOpportunityDto> Opportunities { get; set; } = [];
    
    public List<string> TopMethods { get; set; } = [];
    public List<string> TopDatasets { get; set; } = [];

    public DateTime LastAnalyzedAt { get; set; }
}

public class TimelineDto
{
    public int Year { get; set; }
    public string Achievement { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public int PaperCount { get; set; }
}

public class ResearchOpportunityDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<EvidenceDto> Evidences { get; set; } = [];
}

public class EvidenceDto
{
    public int PaperId { get; set; }
    public string Excerpt { get; set; } = string.Empty;
}
