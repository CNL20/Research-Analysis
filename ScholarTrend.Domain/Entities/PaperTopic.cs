namespace ScholarTrend.Domain.Entities;
public class PaperTopic
{
    public int PaperId { get; set; }
    public int TopicId { get; set; }
    public ResearchPaper Paper { get; set; } = null!;
    public ResearchTopic Topic { get; set; } = null!;
}