namespace ScholarTrend.Application.DTOs.Dashboard;

public class OverviewDashboardDto
{
    public int TotalPapers { get; set; }
    public int TotalKeywords { get; set; }
    public int TotalTopics { get; set; }
    public int TotalJournals { get; set; }
    public int TotalAuthors { get; set; }
    public List<Trends.TrendDataPointDto> PublicationTrend { get; set; } = [];
    public List<Trends.TopTrendItemDto> TopKeywords { get; set; } = [];
    public List<Trends.TopTrendItemDto> TopTopics { get; set; } = [];
}
