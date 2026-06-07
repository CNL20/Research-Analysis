namespace ScholarTrend.Application.DTOs.Papers;

public class AuthorBriefDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Affiliation { get; set; }
}
