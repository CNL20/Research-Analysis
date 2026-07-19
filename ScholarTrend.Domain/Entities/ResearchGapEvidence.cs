namespace ScholarTrend.Domain.Entities;

public class ResearchGapEvidence
{
    public int Id { get; set; }
    public int ResearchGapId { get; set; }
    public ResearchGap ResearchGap { get; set; } = null!;
    
    public int PaperId { get; set; }
    public ResearchPaper Paper { get; set; } = null!;
    
    public string EvidenceSentence { get; set; } = string.Empty;
    public string EvidenceType { get; set; } = string.Empty;
    public string? SectionSource { get; set; }
    public string? PageContext { get; set; }
    public int Confidence { get; set; }
    public bool IsValidated { get; set; }
    public string ValidationStatus { get; set; } = ValidationStatuses.Pending;
}

public static class ValidationStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Disputed = "Disputed";
}

public static class EvidenceTypes
{
    public const string Limitation = "Limitation";
    public const string FutureWork = "FutureWork";
    public const string Discussion = "Discussion";
    public const string Conclusion = "Conclusion";
}
