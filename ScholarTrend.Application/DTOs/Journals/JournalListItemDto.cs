namespace ScholarTrend.Application.DTOs.Journals;

public class JournalListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Publisher { get; set; }
    public string? Issn { get; set; }
    public double? ImpactFactor { get; set; }
    public int PaperCount { get; set; }
}
