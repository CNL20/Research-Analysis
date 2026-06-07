namespace ScholarTrend.Application.DTOs.Trends;

public class TrendDataPointDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int PaperCount { get; set; }
    public int CitationCount { get; set; }
    public double GrowthRate { get; set; }
    public double TrendingScore { get; set; }
}
