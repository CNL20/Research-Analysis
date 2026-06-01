namespace ScholarTrend.Domain.Entities;

public class Journal
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Publisher { get; set; }
    public string? Issn { get; set; }
    public string? Website { get; set; }
    public double? ImpactFactor { get; set; }
    public int? HIndex { get; set; }

    public ICollection<ResearchPaper> Papers { get; set; } = [];
    public ICollection<FollowedJournal> FollowedJournals { get; set; } = [];
    public ICollection<JournalTrend> JournalTrends { get; set; } = [];
}