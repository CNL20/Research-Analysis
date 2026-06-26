using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScholarTrend.Application.DTOs.Trends;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Application.Services;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Tests.Services;

public class TrendServiceTests
{
    private readonly Mock<ITrendRepository> _mockTrendRepo;
    private readonly IMemoryCache _cache;
    private readonly TrendService _trendService;

    public TrendServiceTests()
    {
        _mockTrendRepo = new Mock<ITrendRepository>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _trendService = new TrendService(_mockTrendRepo.Object, _cache);
    }

    [Fact]
    public async Task GetTopKeywordsAsync_ShouldReturnTopKeywords_SortedByTrendingScore()
    {
        // Arrange
        var criteria = new TrendFilterRequest { Top = 2 };
        var keywords = new List<KeywordTrend>
        {
            new() { KeywordId = 1, Keyword = new Keyword { Name = "AI" }, Year = 2025, Month = 5, TrendingScore = 80, GrowthRate = 10 },
            new() { KeywordId = 2, Keyword = new Keyword { Name = "ML" }, Year = 2025, Month = 5, TrendingScore = 95, GrowthRate = 15 },
            new() { KeywordId = 3, Keyword = new Keyword { Name = "Web" }, Year = 2025, Month = 5, TrendingScore = 50, GrowthRate = 5 }
        };

        _mockTrendRepo.Setup(r => r.GetKeywordTrendsAsync(It.IsAny<TrendFilterCriteria>()))
            .ReturnsAsync(keywords);

        // Act
        var result = await _trendService.GetTopKeywordsAsync(criteria);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("ML"); // Highest TrendingScore
        result[1].Name.Should().Be("AI");
        result[0].TrendingScore.Should().Be(95);
    }

    [Fact]
    public async Task GetTopicTrendsAsync_ShouldReturnGroupedTrends()
    {
        // Arrange
        var topicTrends = new List<TopicTrend>
        {
            new() { TopicId = 1, Topic = new ResearchTopic { TopicName = "AI" }, Year = 2025, Month = 1, PaperCount = 10 },
            new() { TopicId = 1, Topic = new ResearchTopic { TopicName = "AI" }, Year = 2025, Month = 2, PaperCount = 15 },
            new() { TopicId = 2, Topic = new ResearchTopic { TopicName = "Data" }, Year = 2025, Month = 1, PaperCount = 5 }
        };

        _mockTrendRepo.Setup(r => r.GetTopicTrendsAsync(It.IsAny<TrendFilterCriteria>()))
            .ReturnsAsync(topicTrends);

        // Act
        var result = await _trendService.GetTopicTrendsAsync(new TrendFilterRequest());

        // Assert
        result.Should().HaveCount(2);
        var aiTrend = result.First(r => r.Name == "AI");
        aiTrend.DataPoints.Should().HaveCount(2);
        aiTrend.DataPoints.First(d => d.Month == 1).PaperCount.Should().Be(10);
        aiTrend.DataPoints.First(d => d.Month == 2).PaperCount.Should().Be(15);
    }

    [Fact]
    public async Task GetDashboardAsync_ShouldReturnCachedData_OnSecondCall()
    {
        // Arrange
        _mockTrendRepo.Setup(r => r.GetKeywordTrendsAsync(It.IsAny<TrendFilterCriteria>()))
            .ReturnsAsync(new List<KeywordTrend>());
        _mockTrendRepo.Setup(r => r.GetTopicTrendsAsync(It.IsAny<TrendFilterCriteria>()))
            .ReturnsAsync(new List<TopicTrend>());
        _mockTrendRepo.Setup(r => r.GetJournalTrendsAsync(It.IsAny<TrendFilterCriteria>()))
            .ReturnsAsync(new List<JournalTrend>());
        _mockTrendRepo.Setup(r => r.GetPublicationTrendAsync(It.IsAny<TrendFilterCriteria>()))
            .ReturnsAsync(new List<TrendDataPointDto>());

        // Act
        await _trendService.GetDashboardAsync();
        await _trendService.GetDashboardAsync();

        // Assert
        _mockTrendRepo.Verify(r => r.GetKeywordTrendsAsync(It.IsAny<TrendFilterCriteria>()), Times.Once);
    }
}
