namespace ScholarTrend.Domain.Entities;
public class PaperKeyword
{
    public int PaperId { get; set; }
    public int KeywordId { get; set; }
    public ResearchPaper Paper { get; set; } = null!;
    public Keyword Keyword { get; set; } = null!;
}