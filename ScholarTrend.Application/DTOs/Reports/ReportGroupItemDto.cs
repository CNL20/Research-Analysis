namespace ScholarTrend.Application.DTOs.Reports;

public class ReportGroupItemDto
{
    public string Key { get; set; } = string.Empty;
    public int PaperCount { get; set; }
    public int TotalCitations { get; set; }
}
