namespace ScholarTrend.Application.DTOs.Trends;

public class TrendDashboardDto
{
    public List<TopTrendItemDto> TopKeywords { get; set; } = [];
    public List<TopTrendItemDto> TopTopics { get; set; } = [];
    public List<TopTrendItemDto> TopJournals { get; set; } = [];
    public List<TrendDataPointDto> PublicationTrend { get; set; } = [];
}
