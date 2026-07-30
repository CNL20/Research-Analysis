namespace ScholarTrend.Application.DTOs.GapAnalysis;

public class ResearchGapReportDto
{
    public int TopicId { get; set; }
    public string TopicName { get; set; } = string.Empty;
    public List<ResearchGapDto> Gaps { get; set; } = [];
    public CoverageReportDto Coverage { get; set; } = new();
    public PatternMiningResultDto Patterns { get; set; } = new();
    public GapTimelineDto Timeline { get; set; } = new();
    public DateTime? GeneratedAt { get; set; }

    /// <summary>cache = read from DB; generated = freshly produced by AI.</summary>
    public string Source { get; set; } = "cache";

    /// <summary>True when paper analyses are newer / more numerous than stored gaps.</summary>
    public bool IsStale { get; set; }

    /// <summary>True when there are no stored gaps yet.</summary>
    public bool NeedsGeneration { get; set; }

    public int AnalysisCount { get; set; }
    public string? StaleReason { get; set; }

    /// <summary>Target sample size (Top N papers for this gap run), typically 150.</summary>
    public int SampleSize { get; set; }

    /// <summary>How many papers in the Top-N sample already have PaperAnalysis.</summary>
    public int AnalyzedInSample { get; set; }

    /// <summary>High | Medium | Low — coverage confidence of the sample.</summary>
    public string SampleCoverageLevel { get; set; } = "Low";

    /// <summary>Short UI label, e.g. "72 / 150 papers analyzed".</summary>
    public string SampleCoverageLabel { get; set; } = string.Empty;

    /// <summary>Optional warning when coverage is low.</summary>
    public string? SampleCoverageMessage { get; set; }

    // Hybrid extraction metadata for the report
    public HybridExtractionStatsDto? HybridStats { get; set; }
}

public static class SampleCoverageLevels
{
    public const string High = "High";
    public const string Medium = "Medium";
    public const string Low = "Low";

    public const int SampleTarget = 10;
    public const int HighThreshold = 100;
    public const int MediumThreshold = 50;

    public static (string Level, string Label, string? Message) FromCounts(int analyzedInSample, int sampleSize)
    {
        var size = sampleSize > 0 ? sampleSize : SampleTarget;
        var level = analyzedInSample >= HighThreshold
            ? High
            : analyzedInSample >= MediumThreshold
                ? Medium
                : Low;

        var label = $"{analyzedInSample} / {size} papers analyzed";
        string? message = level switch
        {
            Low => "The identified research gaps may be incomplete because the available literature analysis is limited.",
            Medium => "Sample coverage is moderate. Extracting more papers in the Top sample can improve gap quality.",
            _ => null
        };

        return (level, label, message);
    }
}

public class ResearchGapDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string GapType { get; set; } = string.Empty;
    public string SuggestedDirection { get; set; } = string.Empty;
    public int EvidenceCount { get; set; }
    public int Confidence { get; set; }
    public string ConfidenceLevel { get; set; } = string.Empty;
    public List<ResearchGapEvidenceDto> Evidences { get; set; } = [];

    // Confidence breakdown by source
    public EvidenceConfidenceBreakdownDto? ConfidenceBreakdown { get; set; }

    // Used internally by the AI service to communicate which Paper IDs back this gap.
    // Not serialized in API responses.
    public List<int> SupportingPaperIds { get; set; } = [];
}

public class EvidenceConfidenceBreakdownDto
{
    public int AbstractEvidenceCount { get; set; }
    public int DiscussionEvidenceCount { get; set; }
    public int ConclusionEvidenceCount { get; set; }
    public int FutureWorkEvidenceCount { get; set; }

    public double AbstractContribution { get; set; }
    public double DiscussionContribution { get; set; }
    public double ConclusionContribution { get; set; }
    public double FutureWorkContribution { get; set; }

    public string MostReliableSource { get; set; } = string.Empty;
}

public class ResearchGapDetailDto : ResearchGapDto
{
    public PatternMiningResultDto SupportingPatterns { get; set; } = new();
    public List<RelatedPaperDto> TopRelatedPapers { get; set; } = [];
    public GapTimelineEntryDto? TrendInfo { get; set; }
}

public class ResearchGapEvidenceDto
{
    public int Id { get; set; }
    public int PaperId { get; set; }
    public string PaperTitle { get; set; } = string.Empty;
    public string Authors { get; set; } = string.Empty;
    public int Year { get; set; }
    public string EvidenceSentence { get; set; } = string.Empty;
    public string EvidenceType { get; set; } = string.Empty;
    public string SectionSource { get; set; } = string.Empty;
    public int Confidence { get; set; }

    // Extended metadata for hybrid extraction
    public string? CitationContext { get; set; }
    public int? AbstractConfidence { get; set; }
    public int? DiscussionConfidence { get; set; }
}

public class RelatedPaperDto
{
    public int PaperId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Authors { get; set; } = string.Empty;
    public int Year { get; set; }
    public int CitationCount { get; set; }
    public string Contribution { get; set; } = string.Empty;
}
