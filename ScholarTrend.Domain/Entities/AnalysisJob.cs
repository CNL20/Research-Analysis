namespace ScholarTrend.Domain.Entities;

public class AnalysisJob
{
    public int Id { get; set; }
    public int PaperId { get; set; }
    public ResearchPaper Paper { get; set; } = null!;
    
    public string Status { get; set; } = AnalysisJobStatus.Pending;
    public string AnalysisType { get; set; } = AnalysisTypes.Abstract;
    public string? ErrorMessage { get; set; }
    
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
}

public static class AnalysisJobStatus
{
    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}

public static class AnalysisTypes
{
    public const string Abstract = "Abstract";
    public const string PdfFullText = "PdfFullText";
}
