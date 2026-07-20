namespace ScholarTrend.Application.DTOs.Migration;

/// <summary>
/// Kết quả của một lần chạy PDF storage migration (local → B2).
/// </summary>
public class PdfMigrationResultDto
{
    /// <summary>Số file đã upload thành công lên B2.</summary>
    public int SuccessCount { get; set; }

    /// <summary>Số file thất bại (xem <see cref="Failures"/> để biết lý do).</summary>
    public int FailureCount { get; set; }

    /// <summary>Số file bị skip (không tồn tại ở local, hoặc không phải local storage).</summary>
    public int SkippedCount { get; set; }

    /// <summary>Tổng số record PaperPdfFile có Status = "Ready" được scan.</summary>
    public int ScannedCount { get; set; }

    /// <summary>Thời gian chạy (ms).</summary>
    public long ElapsedMs { get; set; }

    /// <summary>Chi tiết các lỗi (paperId + lý do).</summary>
    public List<PdfMigrationFailureDto> Failures { get; set; } = new();
}

public class PdfMigrationFailureDto
{
    public int ResearchPaperId { get; set; }
    public string LocalRelativePath { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}