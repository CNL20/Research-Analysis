namespace ScholarTrend.Application.DTOs.Papers;

public class SearchHistoryDto
{
    public int Id { get; set; }
    public string Query { get; set; } = string.Empty;
    public string SearchType { get; set; } = string.Empty;
    public int ResultCount { get; set; }
    public DateTime SearchedAt { get; set; }
}
