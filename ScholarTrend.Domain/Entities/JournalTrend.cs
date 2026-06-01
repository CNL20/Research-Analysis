namespace ScholarTrend.Domain.Entities;
public class JournalTrend
{
    public int Id { get; set; }
    public int JournalId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int PaperCount { get; set; }
    public int CitationCount { get; set; }
    public double GrowthRate { get; set; }
    public double TrendingScore { get; set; }
    public Journal Journal { get; set; } = null!;
}