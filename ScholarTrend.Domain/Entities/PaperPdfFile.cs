using ScholarTrend.Domain.Constants;

namespace ScholarTrend.Domain.Entities;

public class PaperPdfFile
{
    public int Id { get; set; }
    public int ResearchPaperId { get; set; }

    public string ExternalSource { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string LocalRelativePath { get; set; } = string.Empty;

    public long? SizeBytes { get; set; }
    public string? ContentType { get; set; }
    public string? Sha256 { get; set; }

    public string Status { get; set; } = PaperDownloadStatus.Queued;
    public string? FailureReason { get; set; }

    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int AttemptCount { get; set; }

    public ResearchPaper ResearchPaper { get; set; } = null!;
}
