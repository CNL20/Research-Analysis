using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface IResearchPaperRepository : IGenericRepository<ResearchPaper>
{
    Task<IEnumerable<ResearchPaper>> GetPapersByTopicAsync(int topicId);
    Task<IEnumerable<ResearchPaper>> GetPapersByJournalAsync(int journalId);
    Task<IEnumerable<ResearchPaper>> SearchPapersAsync(string searchTerm);
    Task<ResearchPaper?> GetPaperWithDetailsAsync(int id);
}
