namespace ScholarTrend.Domain.Entities;
public class SearchHistory
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string SearchType { get; set; } = string.Empty;
    public int ResultCount { get; set; }
    public int DurationMs { get; set; }
    public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
}