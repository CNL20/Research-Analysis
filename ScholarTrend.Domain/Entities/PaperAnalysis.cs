namespace ScholarTrend.Domain.Entities;

public class PaperAnalysis
{
    public int Id { get; set; }
    public int PaperId { get; set; }
    public ResearchPaper Paper { get; set; } = null!;

    public string? ResearchProblem { get; set; }
    public string? Method { get; set; }
    public string? Dataset { get; set; }
    public string? Metric { get; set; }
    public string? Contribution { get; set; }

    public string? MethodsJson { get; set; }
    public string? DatasetsJson { get; set; }
    public string? LimitationsJson { get; set; }
    public string? FutureWorkJson { get; set; }
    public string? DiscussionsJson { get; set; }
    public string? ConclusionsJson { get; set; }
    public string? KeywordsJson { get; set; }

    public string? EvidenceSentence { get; set; }
    public int Confidence { get; set; }
    public string AnalysisLevel { get; set; } = "Metadata";
    public string AnalysisSource { get; set; } = string.Empty;

    // Hybrid extraction tracking fields
    public string? HybridMetadataJson { get; set; }
    public bool UsedDiscussion { get; set; }
    public bool UsedConclusion { get; set; }
    public bool UsedAbstract { get; set; } = true;
    public int AbstractConfidence { get; set; }
    public int DiscussionConfidence { get; set; }
    public int ConclusionConfidence { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public static class ExtractionSource
{
    public const string AbstractOnly = "Abstract";
    public const string AbstractDiscussion = "Abstract + Discussion";
    public const string AbstractConclusion = "Abstract + Conclusion";
    public const string Hybrid = "Hybrid (Abstract + Sections)";
    public const string FullText = "FullText";
}
