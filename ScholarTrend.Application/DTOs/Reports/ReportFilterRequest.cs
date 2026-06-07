namespace ScholarTrend.Application.DTOs.Reports;

public class ReportFilterRequest
{
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }
    public string GroupBy { get; set; } = "year";
}
