using ScholarTrend.Application.DTOs.Papers;
using ScholarTrend.Application.DTOs.Trends;

namespace ScholarTrend.Application.DTOs.Journals;

public class JournalDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Publisher { get; set; }
    public string? Issn { get; set; }
    public string? Website { get; set; }
    public double? ImpactFactor { get; set; }
    public int? HIndex { get; set; }
    public int PaperCount { get; set; }
    public List<PaperListItemDto> RecentPapers { get; set; } = [];
    public List<TrendDataPointDto> TrendChart { get; set; } = [];
}
