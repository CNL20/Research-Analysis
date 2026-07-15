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

    // --- PDF Text Extraction & Analysis Cache ---
    public string? ExtractedText { get; set; }
    public DateTime? ExtractedAt { get; set; }
    public string? AnalysisResultJson { get; set; }
    public string? AnalysisError { get; set; }
    public string AnalysisStatus { get; set; } = PdfAnalysisStatus.Pending;

    public ResearchPaper ResearchPaper { get; set; } = null!;
}
