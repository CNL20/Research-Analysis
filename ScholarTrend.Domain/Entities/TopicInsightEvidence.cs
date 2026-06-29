namespace ScholarTrend.Domain.Entities;

public class TopicInsightEvidence
{
    public int Id { get; set; }
    
    public int TopicInsightId { get; set; }
    public TopicInsight TopicInsight { get; set; } = null!;
    
    public int PaperId { get; set; }
    public ResearchPaper Paper { get; set; } = null!;
    
    public string EvidenceType { get; set; } = string.Empty; // "Gap" | "FutureWork" | "Achievement"
    public string? Excerpt { get; set; }     
}
