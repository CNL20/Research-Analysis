namespace ScholarTrend.Application.DTOs.Migration;

public class PdfStorageListingDto
{
    public int TotalCount { get; set; }
    public Dictionary<string, int> StatusSummary { get; set; } = new();
    public List<PdfStorageStatusDto> Items { get; set; } = new();
}