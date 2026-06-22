using ScholarTrend.Application.DTOs.Papers;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface IResearchPaperRepository : IGenericRepository<ResearchPaper>
{
    Task<(IReadOnlyList<ResearchPaper> Items, int TotalCount)> SearchAsync(PaperSearchCriteria criteria);
    Task<IEnumerable<ResearchPaper>> GetPapersByTopicAsync(int topicId, int limit = 0);
    Task<IEnumerable<ResearchPaper>> GetPapersByJournalAsync(int journalId, int limit = 0);
    Task<IEnumerable<ResearchPaper>> GetPapersByAuthorAsync(int authorId, int limit = 0);
    Task<ResearchPaper?> GetPaperWithDetailsAsync(int id);
    Task<int> CountByTopicAsync(int topicId);
    Task<int> CountByJournalAsync(int journalId);
    Task<int> CountByAuthorAsync(int authorId);
    Task<ResearchPaper?> GetByExternalIdAsync(string externalId, string source);
    Task<ResearchPaper?> GetByDoiAsync(string doi);
}
