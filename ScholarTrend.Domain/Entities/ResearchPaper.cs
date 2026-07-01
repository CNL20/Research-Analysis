using ScholarTrend.Domain.Enums;

namespace ScholarTrend.Domain.Entities;

public class ResearchPaper
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Abstract { get; set; }
    public int? PublicationYear { get; set; }
    public DateTime? PublicationDate { get; set; }
    public string? Doi { get; set; }
    public string? Url { get; set; }
    public string? PdfUrl { get; set; }
    public string? ExternalId { get; set; }
    public string? ExternalSource { get; set; }
    public int? CitationCount { get; set; }
    public int ViewCount { get; set; } = 0;
    public PaperStatus Status { get; set; } = PaperStatus.Fetched;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public int? JournalId { get; set; }
    public Journal? Journal { get; set; }
    public ICollection<PaperAuthor> PaperAuthors { get; set; } = [];
    public ICollection<PaperKeyword> PaperKeywords { get; set; } = [];
    public ICollection<PaperTopic> PaperTopics { get; set; } = [];
    public ICollection<Bookmark> Bookmarks { get; set; } = [];
    public ICollection<FollowedPaper> FollowedPapers { get; set; } = [];
}