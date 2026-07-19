using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface IResearchGapRepository
{
    Task<ResearchGap?> GetByIdAsync(int id);
    Task<List<ResearchGap>> GetByTopicIdAsync(int topicId);
    Task<ResearchGap?> GetByIdWithEvidencesAsync(int id);
    Task AddAsync(ResearchGap gap);
    Task AddEvidencesAsync(List<ResearchGapEvidence> evidences);
    Task UpdateAsync(ResearchGap gap);
    Task DeleteByTopicAsync(int topicId);
}
