namespace ScholarTrend.Domain.Entities;

public class ResearchGap
{
    public int Id { get; set; }
    public int TopicId { get; set; }
    public ResearchTopic Topic { get; set; } = null!;
    
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string GapType { get; set; } = string.Empty;
    public string SuggestedDirection { get; set; } = string.Empty;
    
    public int EvidenceCount { get; set; }
    public int Confidence { get; set; }
    public string ConfidenceLevel { get; set; } = string.Empty;
    
    public bool IsValidated { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ValidatedAt { get; set; }
    
    public ICollection<ResearchGapEvidence> Evidences { get; set; } = new List<ResearchGapEvidence>();
}

public static class GapTypes
{
    public const string Dataset = "Dataset Gap";
    public const string Method = "Method Gap";
    public const string Evaluation = "Evaluation Gap";
    public const string Application = "Application Gap";
    public const string Geographic = "Geographic Gap";
    public const string Temporal = "Temporal Gap";
    public const string Contradiction = "Contradiction Gap";
    
    public static readonly string[] All = 
    {
        Dataset, Method, Evaluation, Application, Geographic, Temporal, Contradiction
    };
}

public static class ConfidenceLevels
{
    public const string High = "High";
    public const string Medium = "Medium";
    public const string Low = "Low";
    
    public static string GetLevel(int confidence)
    {
        return confidence switch
        {
            >= 80 => High,
            >= 50 => Medium,
            _ => Low
        };
    }
}
