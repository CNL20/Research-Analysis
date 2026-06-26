namespace ScholarTrend.Application.DTOs.Authors;

public class AuthorListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Affiliation { get; set; }
    public string? Country { get; set; }
    public int PaperCount { get; set; }
}
