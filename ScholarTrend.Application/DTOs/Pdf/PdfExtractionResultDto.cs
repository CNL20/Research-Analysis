namespace ScholarTrend.Application.DTOs.Pdf;

/// <summary>
/// Kết quả trích xuất text từ 1 PDF.
/// </summary>
public class PdfExtractionResultDto
{
    public int ResearchPaperId { get; set; }
    public string LocalRelativePath { get; set; } = string.Empty;
    public string? ExtractedText { get; set; }
    public int? CharacterCount { get; set; }
    public string Status { get; set; } = string.Empty;     // "Extracted" | "Failed" | "Empty"
    public string? ErrorMessage { get; set; }
    public DateTime ExtractedAt { get; set; }
}

public class PdfBulkExtractionResultDto
{
    public int Requested { get; set; }
    public int Extracted { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public List<PdfExtractionResultDto> Items { get; set; } = new();
}