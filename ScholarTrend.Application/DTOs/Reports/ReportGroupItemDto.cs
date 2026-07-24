namespace ScholarTrend.Application.DTOs.Reports;

public class ReportGroupItemDto
{
    /// <summary>KeywordId / TopicId / JournalId. Null when GroupBy is year.</summary>
    public int? Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public int PaperCount { get; set; }
    public int TotalCitations { get; set; }

    public int? Rank { get; set; }
    public double? GrowthRate { get; set; }
    public double? TrendingScore { get; set; }
    public int? PeriodYear { get; set; }
    public int? PeriodMonth { get; set; }

    /// <summary>0–100: share of DOI / abstract / journal completeness in the group.</summary>
    public double? ReliabilityPercent { get; set; }

    /// <summary>WorthConsidering | MatureSlowing | EmergingThin | Cooling | Neutral</summary>
    public string? Suggestion { get; set; }
}
