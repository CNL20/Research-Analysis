namespace ScholarTrend.Application.DTOs.Bookmarks;

public class BookmarkDto
{
    public int Id { get; set; }
    public int PaperId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? PublicationYear { get; set; }
    public int? CitationCount { get; set; }
    public string? JournalName { get; set; }
    public DateTime SavedAt { get; set; }
}
