using ScholarTrend.Application.DTOs.GapAnalysis;

namespace ScholarTrend.Application.Interfaces;

public interface IPatternMiningService
{
    Task<PatternMiningResultDto> MinePatternsAsync(int topicId, CancellationToken ct = default);
    Task<Dictionary<string, int>> GetMethodFrequencyAsync(int topicId, int? yearFrom = null, int? yearTo = null);
    Task<Dictionary<string, int>> GetDatasetFrequencyAsync(int topicId, int? yearFrom = null, int? yearTo = null);
    Task<Dictionary<string, int>> GetLimitationFrequencyAsync(int topicId, int? yearFrom = null, int? yearTo = null);
}
