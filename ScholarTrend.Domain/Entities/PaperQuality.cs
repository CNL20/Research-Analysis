namespace ScholarTrend.Domain.Entities;

public class PaperQuality
{
    public int Id { get; set; }
    public int PaperId { get; set; }
    public ResearchPaper Paper { get; set; } = null!;
    
    public bool HasPdf { get; set; }
    public bool HasAbstract { get; set; }
    public bool HasFullText { get; set; }
    public int AbstractLength { get; set; }
    public int AuthorCount { get; set; }
    public bool HasDoi { get; set; }
    public bool HasKeywords { get; set; }
    public bool HasJournal { get; set; }
    public int CitationCount { get; set; }
    public int QualityScore { get; set; }
    public string QualityGrade { get; set; } = string.Empty;
    public string AnalysisLevel { get; set; } = "Metadata";
    public DateTime AssessedAt { get; set; } = DateTime.UtcNow;
}

public static class QualityGrade
{
    public const string A = "A";
    public const string B = "B";
    public const string C = "C";
    public const string D = "D";
    public const string F = "F";
}

public static class AnalysisLevels
{
    public const string Metadata = "Metadata";
    public const string Abstract = "Abstract";
    public const string FullText = "FullText";
}
