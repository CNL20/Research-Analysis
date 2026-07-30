namespace ScholarTrend.Application.DTOs.GapAnalysis;

public class GapGenerationJobDto
{
    public string JobId { get; set; } = string.Empty;
    public int TopicId { get; set; }
    public string Status { get; set; } = GapGenerationStatuses.Queued;
    public string? Message { get; set; }
    public int? GapCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public static class GapGenerationStatuses
{
    public const string Queued = "Queued";
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}
