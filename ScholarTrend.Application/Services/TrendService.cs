using Microsoft.Extensions.Caching.Memory;
using ScholarTrend.Application.DTOs.Trends;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Services;

public class TrendService : ITrendService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private readonly ITrendRepository _trendRepository;
    private readonly IMemoryCache _cache;

    public TrendService(ITrendRepository trendRepository, IMemoryCache cache)
    {
        _trendRepository = trendRepository;
        _cache = cache;
    }

    public Task<TrendDashboardDto> GetDashboardAsync(TrendFilterRequest? filter = null)
    {
        var criteria = NormalizeCriteria(filter);
        var cacheKey = $"trends:dashboard:{BuildCacheKey(criteria)}";

        return _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

            var topKeywords = await GetTopKeywordsInternalAsync(criteria);
            var topTopics = await GetTopTopicsInternalAsync(criteria);
            var topJournals = await GetTopJournalsInternalAsync(criteria);
            var publicationTrend = await _trendRepository.GetPublicationTrendAsync(criteria);

            return new TrendDashboardDto
            {
                TopKeywords = topKeywords.ToList(),
                TopTopics = topTopics.ToList(),
                TopJournals = topJournals.ToList(),
                PublicationTrend = publicationTrend.ToList()
            };
        })!;
    }

    public async Task<IReadOnlyList<TrendSeriesDto>> GetKeywordTrendsAsync(TrendFilterRequest? filter = null)
    {
        var criteria = NormalizeCriteria(filter);
        var trends = await _trendRepository.GetKeywordTrendsAsync(criteria);
        return GroupKeywordTrends(trends);
    }

    public Task<IReadOnlyList<TopTrendItemDto>> GetTopKeywordsAsync(TrendFilterRequest? filter = null)
    {
        var criteria = NormalizeCriteria(filter);
        return GetTopKeywordsInternalAsync(criteria);
    }

    public async Task<IReadOnlyList<TrendSeriesDto>> GetTopicTrendsAsync(TrendFilterRequest? filter = null)
    {
        var criteria = NormalizeCriteria(filter);
        var trends = await _trendRepository.GetTopicTrendsAsync(criteria);
        return GroupTopicTrends(trends);
    }

    public Task<IReadOnlyList<TopTrendItemDto>> GetTopTopicsAsync(TrendFilterRequest? filter = null)
    {
        var criteria = NormalizeCriteria(filter);
        return GetTopTopicsInternalAsync(criteria);
    }

    public async Task<IReadOnlyList<TrendSeriesDto>> GetJournalTrendsAsync(TrendFilterRequest? filter = null)
    {
        var criteria = NormalizeCriteria(filter);
        var trends = await _trendRepository.GetJournalTrendsAsync(criteria);
        return GroupJournalTrends(trends);
    }

    public Task<IReadOnlyList<TopTrendItemDto>> GetTopJournalsAsync(TrendFilterRequest? filter = null)
    {
        var criteria = NormalizeCriteria(filter);
        return GetTopJournalsInternalAsync(criteria);
    }

    public async Task<IReadOnlyList<TrendDataPointDto>> GetPublicationTrendAsync(TrendFilterRequest? filter = null)
    {
        var criteria = NormalizeCriteria(filter);
        return await _trendRepository.GetPublicationTrendAsync(criteria);
    }

    public async Task<IReadOnlyList<TrendSeriesDto>> CompareTrendsAsync(TrendCompareRequest request)
    {
        var type = request.Type.ToLowerInvariant();
        var filter = request.Filter ?? new TrendFilterRequest();
        var criteria = NormalizeCriteria(filter);
        var result = new List<TrendSeriesDto>();

        foreach (var id in request.Ids.Distinct())
        {
            switch (type)
            {
                case "keyword":
                    criteria.KeywordId = id;
                    var keywordTrends = await _trendRepository.GetKeywordTrendsAsync(criteria);
                    result.AddRange(GroupKeywordTrends(keywordTrends));
                    criteria.KeywordId = null;
                    break;
                case "topic":
                    criteria.TopicId = id;
                    var topicTrends = await _trendRepository.GetTopicTrendsAsync(criteria);
                    result.AddRange(GroupTopicTrends(topicTrends));
                    criteria.TopicId = null;
                    break;
                case "journal":
                    criteria.JournalId = id;
                    var journalTrends = await _trendRepository.GetJournalTrendsAsync(criteria);
                    result.AddRange(GroupJournalTrends(journalTrends));
                    criteria.JournalId = null;
                    break;
                default:
                    throw new InvalidOperationException("Type must be keyword, topic, or journal.");
            }
        }

        return result;
    }

    private async Task<IReadOnlyList<TopTrendItemDto>> GetTopKeywordsInternalAsync(TrendFilterCriteria criteria)
    {
        var trends = await _trendRepository.GetKeywordTrendsAsync(criteria);
        return BuildTopItems(
            trends.Select(t => (t.KeywordId, t.Keyword.Name, t.Year, t.Month, t.PaperCount, t.CitationCount, t.GrowthRate, t.TrendingScore)),
            criteria.Top);
    }

    private async Task<IReadOnlyList<TopTrendItemDto>> GetTopTopicsInternalAsync(TrendFilterCriteria criteria)
    {
        var trends = await _trendRepository.GetTopicTrendsAsync(criteria);
        return BuildTopItems(
            trends.Select(t => (t.TopicId, t.Topic.TopicName, t.Year, t.Month, t.PaperCount, t.CitationCount, t.GrowthRate, t.TrendingScore)),
            criteria.Top);
    }

    private async Task<IReadOnlyList<TopTrendItemDto>> GetTopJournalsInternalAsync(TrendFilterCriteria criteria)
    {
        var trends = await _trendRepository.GetJournalTrendsAsync(criteria);
        return BuildTopItems(
            trends.Select(t => (t.JournalId, t.Journal.Name, t.Year, t.Month, t.PaperCount, t.CitationCount, t.GrowthRate, t.TrendingScore)),
            criteria.Top);
    }

    private static IReadOnlyList<TopTrendItemDto> BuildTopItems(
        IEnumerable<(int Id, string Name, int Year, int Month, int PaperCount, int CitationCount, double GrowthRate, double TrendingScore)> trends,
        int top)
    {
        var trendList = trends.ToList();
        if (trendList.Count == 0)
        {
            return [];
        }

        var latestYear = trendList.Max(t => t.Year);
        var latestMonth = trendList.Where(t => t.Year == latestYear).Max(t => t.Month);

        return trendList
            .Where(t => t.Year == latestYear && t.Month == latestMonth)
            .OrderByDescending(t => t.TrendingScore)
            .ThenByDescending(t => t.GrowthRate)
            .Take(top)
            .Select(t => new TopTrendItemDto
            {
                Id = t.Id,
                Name = t.Name,
                PaperCount = t.PaperCount,
                CitationCount = t.CitationCount,
                GrowthRate = t.GrowthRate,
                TrendingScore = t.TrendingScore,
                Year = t.Year,
                Month = t.Month
            })
            .ToList();
    }

    private static IReadOnlyList<TrendSeriesDto> GroupKeywordTrends(IReadOnlyList<KeywordTrend> trends)
    {
        return trends
            .GroupBy(t => new { t.KeywordId, t.Keyword.Name })
            .Select(g => new TrendSeriesDto
            {
                Id = g.Key.KeywordId,
                Name = g.Key.Name,
                Type = "keyword",
                DataPoints = g.Select(t => MapDataPoint(t)).ToList()
            })
            .ToList();
    }

    private static IReadOnlyList<TrendSeriesDto> GroupTopicTrends(IReadOnlyList<TopicTrend> trends)
    {
        return trends
            .GroupBy(t => new { t.TopicId, t.Topic.TopicName })
            .Select(g => new TrendSeriesDto
            {
                Id = g.Key.TopicId,
                Name = g.Key.TopicName,
                Type = "topic",
                DataPoints = g.Select(t => MapDataPoint(t)).ToList()
            })
            .ToList();
    }

    private static IReadOnlyList<TrendSeriesDto> GroupJournalTrends(IReadOnlyList<JournalTrend> trends)
    {
        return trends
            .GroupBy(t => new { t.JournalId, t.Journal.Name })
            .Select(g => new TrendSeriesDto
            {
                Id = g.Key.JournalId,
                Name = g.Key.Name,
                Type = "journal",
                DataPoints = g.Select(t => MapDataPoint(t)).ToList()
            })
            .ToList();
    }

    private static TrendDataPointDto MapDataPoint(KeywordTrend trend) => new()
    {
        Year = trend.Year,
        Month = trend.Month,
        PaperCount = trend.PaperCount,
        CitationCount = trend.CitationCount,
        GrowthRate = trend.GrowthRate,
        TrendingScore = trend.TrendingScore
    };

    private static TrendDataPointDto MapDataPoint(TopicTrend trend) => new()
    {
        Year = trend.Year,
        Month = trend.Month,
        PaperCount = trend.PaperCount,
        CitationCount = trend.CitationCount,
        GrowthRate = trend.GrowthRate,
        TrendingScore = trend.TrendingScore
    };

    private static TrendDataPointDto MapDataPoint(JournalTrend trend) => new()
    {
        Year = trend.Year,
        Month = trend.Month,
        PaperCount = trend.PaperCount,
        CitationCount = trend.CitationCount,
        GrowthRate = trend.GrowthRate,
        TrendingScore = trend.TrendingScore
    };

    private static TrendFilterCriteria NormalizeCriteria(TrendFilterRequest? filter)
    {
        filter ??= new TrendFilterRequest();

        return new TrendFilterCriteria
        {
            YearFrom = filter.YearFrom ?? 2025,
            YearTo = filter.YearTo ?? 2026,
            MonthFrom = filter.MonthFrom,
            MonthTo = filter.MonthTo,
            KeywordId = filter.KeywordId,
            TopicId = filter.TopicId,
            JournalId = filter.JournalId,
            Top = filter.Top is > 0 and <= 50 ? filter.Top : 10
        };
    }

    private static string BuildCacheKey(TrendFilterCriteria criteria)
    {
        return $"{criteria.YearFrom}-{criteria.MonthFrom}-{criteria.YearTo}-{criteria.MonthTo}-{criteria.Top}";
    }
}
