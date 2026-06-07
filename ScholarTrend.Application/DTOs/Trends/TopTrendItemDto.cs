namespace ScholarTrend.Application.DTOs.Trends;

public class TopTrendItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int PaperCount { get; set; }
    public int CitationCount { get; set; }
    public double GrowthRate { get; set; }
    public double TrendingScore { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
}
