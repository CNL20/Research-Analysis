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
