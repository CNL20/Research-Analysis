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
    Task<Dictionary<int, int>> CountByTopicIdsAsync(IEnumerable<int> topicIds);
    Task<int> CountByJournalAsync(int journalId);
    Task<int> CountByAuthorAsync(int authorId);
    Task<Dictionary<int, int>> CountByAuthorIdsAsync(IEnumerable<int> authorIds);
    Task<ResearchPaper?> GetByExternalIdAsync(string externalId, string source);
    Task<ResearchPaper?> GetByDoiAsync(string doi);

    /// <summary>
    /// Top paper ids for gap/extract sampling: abstract required, ordered by recency then citations.
    /// Excludes absurd future publication years (bad metadata) that starve the sample of real analyses.
    /// </summary>
    Task<List<int>> GetTopPaperIdsForTopicSampleAsync(int topicId, int take);

    /// <summary>
    /// Top papers that already have PaperAnalysis (for gap gen when the raw Top-N has almost none).
    /// </summary>
    Task<List<int>> GetTopAnalyzedPaperIdsForTopicSampleAsync(int topicId, int take);
}
