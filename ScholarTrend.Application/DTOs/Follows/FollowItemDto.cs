namespace ScholarTrend.Application.DTOs.Follows;

public class FollowItemDto
{
    public int Id { get; set; }
    public int TargetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime FollowedAt { get; set; }
}
