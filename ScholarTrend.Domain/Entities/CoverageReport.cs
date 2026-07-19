namespace ScholarTrend.Domain.Entities;

public class CoverageReport
{
    public int Id { get; set; }
    public int TopicId { get; set; }
    public ResearchTopic Topic { get; set; } = null!;
    
    public int TotalPapers { get; set; }
    public int PdfAnalyzedPapers { get; set; }
    public int AbstractAnalyzedPapers { get; set; }
    public int MetadataOnlyPapers { get; set; }
    public int IgnoredPapers { get; set; }
    
    public double CoveragePercentage { get; set; }
    public double AbstractCoveragePercentage { get; set; }
    public double FullTextCoveragePercentage { get; set; }
    
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
