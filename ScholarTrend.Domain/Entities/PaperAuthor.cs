namespace ScholarTrend.Domain.Entities;
public class PaperAuthor
{
    public int PaperId { get; set; }
    public int AuthorId { get; set; }
    public int AuthorOrder { get; set; } = 0;
    public ResearchPaper Paper { get; set; } = null!;
    public Author Author { get; set; } = null!;
}