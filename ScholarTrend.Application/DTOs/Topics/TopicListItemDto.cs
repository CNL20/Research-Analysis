namespace ScholarTrend.Application.DTOs.Topics;

public class TopicListItemDto
{
    public int Id { get; set; }
    public string TopicName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PaperCount { get; set; }
}
