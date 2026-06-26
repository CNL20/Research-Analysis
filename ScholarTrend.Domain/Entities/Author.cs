namespace ScholarTrend.Domain.Entities;

public class Author
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public string? Affiliation { get; set; }
    public string? Country { get; set; }
    public int? HIndex { get; set; }
    public int? TotalCitations { get; set; }

    public ICollection<PaperAuthor> PaperAuthors { get; set; } = [];
    public ICollection<FollowedAuthor> FollowedAuthors { get; set; } = [];
}