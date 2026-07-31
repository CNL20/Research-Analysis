using ScholarTrend.Application.DTOs.GapAnalysis;

namespace ScholarTrend.Application.Interfaces;

public interface IPatternMiningService
{
    Task<PatternMiningResultDto> MinePatternsAsync(int topicId, CancellationToken ct = default);

    /// <summary>Mine + upsert patterns using only the given paper ids (gap Top-N sample).</summary>
    Task<PatternMiningResultDto> MinePatternsForPaperIdsAsync(
        int topicId,
        IReadOnlyCollection<int> paperIds,
        CancellationToken ct = default);

    /// <summary>Read patterns already stored in DB (no remine).</summary>
    Task<PatternMiningResultDto> GetStoredPatternsAsync(int topicId, CancellationToken ct = default);

    Task<Dictionary<string, int>> GetMethodFrequencyAsync(int topicId, int? yearFrom = null, int? yearTo = null);
    Task<Dictionary<string, int>> GetDatasetFrequencyAsync(int topicId, int? yearFrom = null, int? yearTo = null);
    Task<Dictionary<string, int>> GetLimitationFrequencyAsync(int topicId, int? yearFrom = null, int? yearTo = null);
}
