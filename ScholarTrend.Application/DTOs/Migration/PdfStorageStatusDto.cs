namespace ScholarTrend.Application.DTOs.Migration;

/// <summary>
/// Thông tin tổng quan về 1 PDF trong hệ thống (dùng cho admin dashboard/verification).
/// </summary>
public class PdfStorageStatusDto
{
    public int PaperPdfFileId { get; set; }
    public int ResearchPaperId { get; set; }
    public string PaperTitle { get; set; } = string.Empty;
    public string LocalRelativePath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long? SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public string? ExternalSource { get; set; }
    public string? SourceUrl { get; set; }
    public int AttemptCount { get; set; }
    public DateTime EnqueuedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? FailureReason { get; set; }

    /// <summary>
    /// True nếu Status=Ready VÀ size > 0 (file đã được tải về thật sự).
    /// </summary>
    public bool IsFileAvailable => Status == "Ready" && SizeBytes > 0;
}