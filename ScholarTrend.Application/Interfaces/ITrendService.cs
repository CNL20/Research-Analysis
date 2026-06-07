using ScholarTrend.Application.DTOs.Trends;

namespace ScholarTrend.Application.Interfaces;

public interface ITrendService
{
    Task<TrendDashboardDto> GetDashboardAsync(TrendFilterRequest? filter = null);
    Task<IReadOnlyList<TrendSeriesDto>> GetKeywordTrendsAsync(TrendFilterRequest? filter = null);
    Task<IReadOnlyList<TopTrendItemDto>> GetTopKeywordsAsync(TrendFilterRequest? filter = null);
    Task<IReadOnlyList<TrendSeriesDto>> GetTopicTrendsAsync(TrendFilterRequest? filter = null);
    Task<IReadOnlyList<TopTrendItemDto>> GetTopTopicsAsync(TrendFilterRequest? filter = null);
    Task<IReadOnlyList<TrendSeriesDto>> GetJournalTrendsAsync(TrendFilterRequest? filter = null);
    Task<IReadOnlyList<TopTrendItemDto>> GetTopJournalsAsync(TrendFilterRequest? filter = null);
    Task<IReadOnlyList<TrendDataPointDto>> GetPublicationTrendAsync(TrendFilterRequest? filter = null);
    Task<IReadOnlyList<TrendSeriesDto>> CompareTrendsAsync(TrendCompareRequest request);
}
