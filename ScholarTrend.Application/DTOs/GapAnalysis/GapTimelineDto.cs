namespace ScholarTrend.Application.DTOs.GapAnalysis;

public class GapTimelineDto
{
    public int TopicId { get; set; }
    public string TopicName { get; set; } = string.Empty;
    public List<GapTimelineEntryDto> Timeline { get; set; } = [];
}

public class GapTimelineEntryDto
{
    public int Year { get; set; }
    public string GapType { get; set; } = string.Empty;
    public string GapTitle { get; set; } = string.Empty;
    public int PaperCount { get; set; }
    public bool IsResolved { get; set; }
    public string Trend { get; set; } = "stable";
    public double GrowthRate { get; set; }
}

public class TrendAnalysisResultDto
{
    public int TopicId { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public List<TrendDataPointDto> DataPoints { get; set; } = [];
    public string OverallTrend { get; set; } = "stable";
    public double GrowthRate { get; set; }
    public string Status { get; set; } = "stable";
}

public class TrendDataPointDto
{
    public int Year { get; set; }
    public int PaperCount { get; set; }
    public double GrowthRate { get; set; }
}
