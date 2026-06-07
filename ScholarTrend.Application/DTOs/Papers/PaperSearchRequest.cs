namespace ScholarTrend.Application.DTOs.Papers;

public class PaperSearchRequest
{
    public string? Query { get; set; }
    public string SearchType { get; set; } = "keyword";
    public int? JournalId { get; set; }
    public int? TopicId { get; set; }
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }
    public int? MinCitations { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
