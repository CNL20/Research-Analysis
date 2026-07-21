namespace ScholarTrend.Application.DTOs.GapAnalysis;

public class ResearchGapReportDto
{
    public int TopicId { get; set; }
    public string TopicName { get; set; } = string.Empty;
    public List<ResearchGapDto> Gaps { get; set; } = [];
    public CoverageReportDto Coverage { get; set; } = new();
    public PatternMiningResultDto Patterns { get; set; } = new();
    public GapTimelineDto Timeline { get; set; } = new();
    public DateTime GeneratedAt { get; set; }

    // Hybrid extraction metadata for the report
    public HybridExtractionStatsDto? HybridStats { get; set; }
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
