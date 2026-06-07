using ScholarTrend.Application.DTOs.Trends;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface ITrendRepository
{
    Task<IReadOnlyList<KeywordTrend>> GetKeywordTrendsAsync(TrendFilterCriteria criteria);
    Task<IReadOnlyList<TopicTrend>> GetTopicTrendsAsync(TrendFilterCriteria criteria);
    Task<IReadOnlyList<JournalTrend>> GetJournalTrendsAsync(TrendFilterCriteria criteria);
    Task<IReadOnlyList<TrendDataPointDto>> GetPublicationTrendAsync(TrendFilterCriteria criteria);
}
