using ScholarTrend.Application.DTOs.GapAnalysis;

namespace ScholarTrend.Application.Interfaces;

public interface ITrendAnalysisService
{
    Task<GapTimelineDto> GetGapTimelineAsync(int topicId);
    Task<TrendAnalysisResultDto> AnalyzeMethodTrendAsync(int topicId, string methodName);
    Task<TrendAnalysisResultDto> AnalyzeDatasetTrendAsync(int topicId, string datasetName);
    Task<TrendAnalysisResultDto> AnalyzeLimitationTrendAsync(int topicId, string limitationKeyword);
    Task<List<TrendAnalysisResultDto>> GetTopMethodTrendsAsync(int topicId, int top = 10);
}
