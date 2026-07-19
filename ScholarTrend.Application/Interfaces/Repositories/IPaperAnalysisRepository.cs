using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface IPaperAnalysisRepository
{
    Task<PaperAnalysis?> GetByIdAsync(int id);
    Task<PaperAnalysis?> GetByPaperIdAsync(int paperId);
    Task<List<PaperAnalysis>> GetByTopicIdAsync(int topicId);
    Task<List<PaperAnalysis>> GetByTopicIdWithLimitAsync(int topicId, int limit);
    Task<List<PaperAnalysis>> GetAnalyzedPapersWithoutFullTextAsync(int topicId, int take = 50);
    Task<PaperAnalysis> UpsertAsync(PaperAnalysis analysis);
    Task AddAsync(PaperAnalysis analysis);
    void Update(PaperAnalysis analysis);
}
