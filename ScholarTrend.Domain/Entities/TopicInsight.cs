namespace ScholarTrend.Domain.Entities;

public class TopicInsight
{
    public int Id { get; set; }
    public int TopicId { get; set; }
    public ResearchTopic Topic { get; set; } = null!;
    
    public int Year { get; set; }

    public string Achievement { get; set; } = string.Empty; 
    public string Summary { get; set; } = string.Empty;

    public string? ResearchGapsJson { get; set; }       
    public string? FutureDirectionsJson { get; set; }   
    
    public string? TopMethodsJson { get; set; }         
    public string? TopDatasetsJson { get; set; }

    public int PaperCountAtGeneration { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<TopicInsightEvidence> Evidences { get; set; } = [];
}
