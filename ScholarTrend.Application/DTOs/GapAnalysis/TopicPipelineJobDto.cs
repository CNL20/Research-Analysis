namespace ScholarTrend.Application.DTOs.GapAnalysis;

public static class TopicPipelineStatuses
{
    public const string Queued = "Queued";
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}

public static class TopicPipelineSteps
{
    public const string Queued = "Queued";
    public const string Quality = "Quality";
    public const string Extraction = "Extraction";
    public const string Patterns = "Patterns";
    public const string Gaps = "Gaps";
    public const string Done = "Done";
}

public class TopicPipelineJobDto
{
    public string JobId { get; set; } = string.Empty;
    public int TopicId { get; set; }
    public string Status { get; set; } = TopicPipelineStatuses.Queued;
    public string Step { get; set; } = TopicPipelineSteps.Queued;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
