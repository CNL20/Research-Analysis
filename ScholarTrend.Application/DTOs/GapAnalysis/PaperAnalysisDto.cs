namespace ScholarTrend.Application.DTOs.GapAnalysis;

public class GapAnalysisResultDto
{
    public int TopicId { get; set; }
    public string TopicName { get; set; } = string.Empty;
    public int TotalPapers { get; set; }
    public int AnalyzedPapers { get; set; }
    public int PendingPapers { get; set; }
    public int FailedPapers { get; set; }
    public double AnalysisProgress { get; set; }
    public List<PaperAnalysisDto> RecentAnalyses { get; set; } = [];

    // Hybrid extraction statistics
    public HybridExtractionStatsDto? HybridStats { get; set; }
}

public class HybridExtractionStatsDto
{
    public int HybridAnalyzedPapers { get; set; }
    public int AbstractOnlyPapers { get; set; }
    public int UsedDiscussionPapers { get; set; }
    public int UsedConclusionPapers { get; set; }
    public double AverageAbstractConfidence { get; set; }
    public double AverageDiscussionConfidence { get; set; }
    public double AverageOverallConfidence { get; set; }
}

public class PaperAnalysisDto
{
    public int PaperId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? ResearchProblem { get; set; }
    public string? Method { get; set; }
    public string? Dataset { get; set; }
    public string? Metric { get; set; }
    public string? Contribution { get; set; }
    public List<string> Methods { get; set; } = [];
    public List<string> Datasets { get; set; } = [];
    public List<string> Limitations { get; set; } = [];
    public List<string> FutureWork { get; set; } = [];
    public List<string> Discussions { get; set; } = [];
    public List<string> Conclusions { get; set; } = [];
    public string AnalysisLevel { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public DateTime AnalyzedAt { get; set; }

    // Hybrid extraction metadata
    public string AnalysisSource { get; set; } = string.Empty;
    public bool UsedDiscussion { get; set; }
    public bool UsedConclusion { get; set; }
    public int AbstractConfidence { get; set; }
    public int DiscussionConfidence { get; set; }
    public int ConclusionConfidence { get; set; }
}
