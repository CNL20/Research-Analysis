namespace ScholarTrend.Application.DTOs.ExternalApis;

public class ExternalPaperDto
{
    public string ExternalId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Abstract { get; set; }
    public int? Year { get; set; }
    public int? CitationCount { get; set; }
    public string? Doi { get; set; }
    public string? Url { get; set; }
    public List<string> AuthorNames { get; set; } = [];
}
