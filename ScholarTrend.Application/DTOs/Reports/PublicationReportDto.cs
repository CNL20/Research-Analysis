namespace ScholarTrend.Application.DTOs.Reports;

public class PublicationReportDto
{
    public string GroupBy { get; set; } = string.Empty;
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }
    public int TotalPapers { get; set; }
    public List<ReportGroupItemDto> Items { get; set; } = [];
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
