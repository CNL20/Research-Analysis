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
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
