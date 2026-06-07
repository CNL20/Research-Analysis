namespace ScholarTrend.Application.DTOs.Trends;

public class TrendFilterCriteria
{
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }
    public int? MonthFrom { get; set; }
    public int? MonthTo { get; set; }
    public int? KeywordId { get; set; }
    public int? TopicId { get; set; }
    public int? JournalId { get; set; }
    public int Top { get; set; } = 10;
}
