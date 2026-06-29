namespace ScholarTrend.Domain.Entities;

public class PaperTopicExtraction
{
    public int Id { get; set; }
    public int PaperId { get; set; }
    public ResearchPaper Paper { get; set; } = null!;
    
    public int TopicId { get; set; }
    public ResearchTopic Topic { get; set; } = null!;

    // Structured JSON extractions from LLM
    public string? MethodsJson { get; set; }     // e.g. ["CNN", "ResNet"]
    public string? DatasetsJson { get; set; }    // e.g. ["ImageNet"]
    public string? LimitationsJson { get; set; } // Paper limitations
    public string? FutureWorkJson { get; set; }  // Future directions proposed
    public string? AchievementHint { get; set; } // Core contribution

    public DateTime ExtractedAt { get; set; }
}
