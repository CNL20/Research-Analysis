using ScholarTrend.Application.DTOs.Papers;
using ScholarTrend.Application.DTOs.Trends;

namespace ScholarTrend.Application.DTOs.Topics;

public class TopicDetailDto
{
    public int Id { get; set; }
    public string TopicName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PaperCount { get; set; }
    public List<PaperListItemDto> RecentPapers { get; set; } = [];
    public List<TrendDataPointDto> TrendChart { get; set; } = [];
}
