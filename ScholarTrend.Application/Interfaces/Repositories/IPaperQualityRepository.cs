using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface IPaperQualityRepository
{
    Task<PaperQuality?> GetByIdAsync(int id);
    Task<PaperQuality?> GetByPaperIdAsync(int paperId);
    Task<List<PaperQuality>> GetByTopicIdAsync(int topicId);
    Task<List<PaperQuality>> GetByAnalysisLevelAsync(string level, int take = 100);
    Task<PaperQuality> UpsertAsync(PaperQuality quality);
    Task AddAsync(PaperQuality quality);
    void Update(PaperQuality quality);
}
