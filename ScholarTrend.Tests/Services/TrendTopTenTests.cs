using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScholarTrend.Application.DTOs.Trends;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Application.Services;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Tests.Services;

public class TrendTopTenTests
{
    private readonly Mock<ITrendRepository> _mockTrendRepo;
    private readonly TrendService _trendService;

    public TrendTopTenTests()
    {
        _mockTrendRepo = new Mock<ITrendRepository>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var invalidator = new TrendDashboardCacheInvalidator(cache);
        _trendService = new TrendService(_mockTrendRepo.Object, cache, invalidator);
    }

    [Fact]
    public async Task GetTopTopicsAsync_DefaultTop_ReturnsTenItems()
    {
        var topics = Enumerable.Range(1, 15).Select(i => new TopicTrend
        {
            TopicId = i,
            Topic = new ResearchTopic { TopicName = $"Topic {i}" },
            Year = 2025,
            Month = 6,
            PaperCount = i,
            TrendingScore = i * 10
        }).ToList();

        _mockTrendRepo.Setup(r => r.GetTopicTrendsAsync(It.IsAny<TrendFilterCriteria>()))
            .ReturnsAsync(topics);

        var result = await _trendService.GetTopTopicsAsync(new TrendFilterRequest());

        result.Should().HaveCount(10);
        result[0].Name.Should().Be("Topic 15");
        result[9].Name.Should().Be("Topic 6");
    }

    [Fact]
    public async Task GetDashboardAsync_ReturnsTopTen_ForKeywordsTopicsJournals()
    {
        _mockTrendRepo.Setup(r => r.GetKeywordTrendsAsync(It.IsAny<TrendFilterCriteria>()))
            .ReturnsAsync(MakeKeywordTrends(12));
        _mockTrendRepo.Setup(r => r.GetTopicTrendsAsync(It.IsAny<TrendFilterCriteria>()))
            .ReturnsAsync(MakeTopicTrends(12));
        _mockTrendRepo.Setup(r => r.GetJournalTrendsAsync(It.IsAny<TrendFilterCriteria>()))
            .ReturnsAsync(MakeJournalTrends(12));
        _mockTrendRepo.Setup(r => r.GetPublicationTrendAsync(It.IsAny<TrendFilterCriteria>()))
            .ReturnsAsync([]);

        var dashboard = await _trendService.GetDashboardAsync(new TrendFilterRequest { Top = 10 });

        dashboard.TopKeywords.Should().HaveCount(10);
        dashboard.TopTopics.Should().HaveCount(10);
        dashboard.TopJournals.Should().HaveCount(10);
    }

    [Fact]
    public async Task GetTopKeywordsAsync_WithYearFilter_PassesCriteriaToRepository()
    {
        TrendFilterCriteria? captured = null;
        _mockTrendRepo.Setup(r => r.GetKeywordTrendsAsync(It.IsAny<TrendFilterCriteria>()))
            .Callback<TrendFilterCriteria>(c => captured = c)
            .ReturnsAsync([]);

        await _trendService.GetTopKeywordsAsync(new TrendFilterRequest
        {
            YearFrom = 2024,
            YearTo = 2025,
            MonthFrom = 1,
            MonthTo = 12,
            Top = 10
        });

        captured.Should().NotBeNull();
        captured!.YearFrom.Should().Be(2024);
        captured.YearTo.Should().Be(2025);
        captured.MonthFrom.Should().Be(1);
        captured.MonthTo.Should().Be(12);
        captured.Top.Should().Be(10);
    }

    private static List<KeywordTrend> MakeKeywordTrends(int count) =>
        Enumerable.Range(1, count).Select(i => new KeywordTrend
        {
            KeywordId = i,
            Keyword = new Keyword { Name = $"KW {i}" },
            Year = 2025,
            Month = 6,
            PaperCount = i,
            TrendingScore = i
        }).ToList();

    private static List<TopicTrend> MakeTopicTrends(int count) =>
        Enumerable.Range(1, count).Select(i => new TopicTrend
        {
            TopicId = i,
            Topic = new ResearchTopic { TopicName = $"Topic {i}" },
            Year = 2025,
            Month = 6,
            PaperCount = i,
            TrendingScore = i
        }).ToList();

    private static List<JournalTrend> MakeJournalTrends(int count) =>
        Enumerable.Range(1, count).Select(i => new JournalTrend
        {
            JournalId = i,
            Journal = new Journal { Name = $"Journal {i}" },
            Year = 2025,
            Month = 6,
            PaperCount = i,
            TrendingScore = i
        }).ToList();
}
