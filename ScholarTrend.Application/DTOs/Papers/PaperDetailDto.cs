namespace ScholarTrend.Application.DTOs.Papers;

public class PaperDetailDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Abstract { get; set; }
    public int? PublicationYear { get; set; }
    public DateTime? PublicationDate { get; set; }
    public int? CitationCount { get; set; }
    public int ViewCount { get; set; }
    public string? Doi { get; set; }
    public string? Url { get; set; }
    public string? PdfUrl { get; set; }
    public JournalBriefDto? Journal { get; set; }
    public List<AuthorBriefDto> Authors { get; set; } = [];
    public List<string> Keywords { get; set; } = [];
    public List<string> Topics { get; set; } = [];
    public bool IsBookmarked { get; set; }
}
