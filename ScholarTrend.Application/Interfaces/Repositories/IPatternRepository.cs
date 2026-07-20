using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface IPatternRepository
{
    Task<List<MethodPattern>> GetMethodPatternsAsync(int topicId, int? yearFrom = null, int? yearTo = null);
    Task<List<DatasetPattern>> GetDatasetPatternsAsync(int topicId, int? yearFrom = null, int? yearTo = null);
    Task<List<LimitationPattern>> GetLimitationPatternsAsync(int topicId, int? yearFrom = null, int? yearTo = null);
    Task UpsertMethodPatternsAsync(List<MethodPattern> patterns);
    Task UpsertDatasetPatternsAsync(List<DatasetPattern> patterns);
    Task UpsertLimitationPatternsAsync(List<LimitationPattern> patterns);
    Task DeleteByTopicAsync(int topicId);
}
