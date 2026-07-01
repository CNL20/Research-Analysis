namespace ScholarTrend.Application.DTOs.Papers;

public class PaperListItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Abstract { get; set; }
    public int? PublicationYear { get; set; }
    public int? CitationCount { get; set; }
    public int ViewCount { get; set; }
    public string? Doi { get; set; }
    public JournalBriefDto? Journal { get; set; }
    public List<string> Authors { get; set; } = [];
    public List<string> Keywords { get; set; } = [];
}
