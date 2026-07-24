namespace ScholarTrend.Application.DTOs.Reports;

public class ReportFilterRequest
{
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }
    public string GroupBy { get; set; } = "year";

    /// <summary>Optional cap after ranking (e.g. top 5 keywords). Null = return all groups.</summary>
    public int? Top { get; set; }
}
