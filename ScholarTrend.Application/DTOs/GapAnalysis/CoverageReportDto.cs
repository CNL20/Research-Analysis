namespace ScholarTrend.Application.DTOs.GapAnalysis;

public class CoverageReportDto
{
    public int TopicId { get; set; }
    public string TopicName { get; set; } = string.Empty;
    public int TotalPapers { get; set; }
    public int PdfAnalyzedPapers { get; set; }
    public int AbstractAnalyzedPapers { get; set; }
    public int MetadataOnlyPapers { get; set; }
    public int IgnoredPapers { get; set; }
    public double CoveragePercentage { get; set; }
    public double AbstractCoveragePercentage { get; set; }
    public double FullTextCoveragePercentage { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class PaperQualityReportDto
{
    public int TopicId { get; set; }
    public string TopicName { get; set; } = string.Empty;
    public int TotalPapers { get; set; }
    public int GradeACount { get; set; }
    public int GradeBCount { get; set; }
    public int GradeCCount { get; set; }
    public int GradeDCount { get; set; }
    public int GradeFCount { get; set; }
    public double AverageQualityScore { get; set; }
    public Dictionary<string, int> AnalysisLevelBreakdown { get; set; } = new();
}
