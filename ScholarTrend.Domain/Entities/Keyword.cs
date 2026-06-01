namespace ScholarTrend.Domain.Entities;

public class Keyword
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PaperKeyword> PaperKeywords { get; set; } = [];
    public ICollection<KeywordTrend> KeywordTrends { get; set; } = [];
}