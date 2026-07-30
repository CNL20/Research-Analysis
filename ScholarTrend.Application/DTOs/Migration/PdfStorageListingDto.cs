namespace ScholarTrend.Application.DTOs.Migration;

public class PdfStorageListingDto
{
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public Dictionary<string, int> StatusSummary { get; set; } = new();
    public List<PdfStorageStatusDto> Items { get; set; } = new();
}