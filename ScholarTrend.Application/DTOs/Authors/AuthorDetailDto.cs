using ScholarTrend.Application.DTOs.Papers;

namespace ScholarTrend.Application.DTOs.Authors;

public class AuthorDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public string? Affiliation { get; set; }
    public string? Country { get; set; }
    public int? HIndex { get; set; }
    public int? TotalCitations { get; set; }
    public int PaperCount { get; set; }
    public List<PaperListItemDto> RecentPapers { get; set; } = [];
}
