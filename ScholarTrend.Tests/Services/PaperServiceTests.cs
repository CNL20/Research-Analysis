using FluentAssertions;
using Moq;
using ScholarTrend.Application.DTOs.Papers;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Application.Services;
using ScholarTrend.Domain.Entities;
using Xunit;

namespace ScholarTrend.Tests.Services;

public class PaperServiceTests
{
    private readonly Mock<IResearchPaperRepository> _mockPaperRepo;
    private readonly Mock<IBookmarkRepository> _mockBookmarkRepo;
    private readonly Mock<IResearchTopicRepository> _mockTopicRepo;
    private readonly Mock<IJournalRepository> _mockJournalRepo;
    private readonly Mock<ISearchHistoryRepository> _mockSearchHistoryRepo;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly PaperService _paperService;

    public PaperServiceTests()
    {
        _mockPaperRepo = new Mock<IResearchPaperRepository>();
        _mockBookmarkRepo = new Mock<IBookmarkRepository>();
        _mockTopicRepo = new Mock<IResearchTopicRepository>();
        _mockJournalRepo = new Mock<IJournalRepository>();
        _mockSearchHistoryRepo = new Mock<ISearchHistoryRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _mockUnitOfWork.Setup(u => u.ResearchPapers).Returns(_mockPaperRepo.Object);
        _mockUnitOfWork.Setup(u => u.Bookmarks).Returns(_mockBookmarkRepo.Object);
        _mockUnitOfWork.Setup(u => u.Topics).Returns(_mockTopicRepo.Object);
        _mockUnitOfWork.Setup(u => u.Journals).Returns(_mockJournalRepo.Object);
        _mockUnitOfWork.Setup(u => u.SearchHistories).Returns(_mockSearchHistoryRepo.Object);

        _paperService = new PaperService(_mockUnitOfWork.Object);
    }

    // ─────────────────────────────────────────────────────────────
    // SearchAsync
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_ShouldReturnPagedResult_WhenPapersFound()
    {
        // Arrange
        var userId = "user-001";
        var papers = new List<ResearchPaper>
        {
            new() { Id = 1, Title = "AI in Healthcare", PublicationYear = 2023 },
            new() { Id = 2, Title = "Deep Learning for NLP", PublicationYear = 2024 }
        };
        var request = new PaperSearchRequest { Query = "AI", Page = 1, PageSize = 10 };

        _mockPaperRepo.Setup(r => r.SearchAsync(It.IsAny<PaperSearchCriteria>()))
            .ReturnsAsync((papers, 2));
        _mockSearchHistoryRepo.Setup(r => r.AddAsync(It.IsAny<SearchHistory>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _paperService.SearchAsync(request, userId);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnEmptyResult_WhenNoPapersFound()
    {
        // Arrange
        var userId = "user-001";
        var request = new PaperSearchRequest { Query = "nonexistent-keyword-xyz", Page = 1, PageSize = 10 };

        _mockPaperRepo.Setup(r => r.SearchAsync(It.IsAny<PaperSearchCriteria>()))
            .ReturnsAsync((new List<ResearchPaper>(), 0));
        _mockSearchHistoryRepo.Setup(r => r.AddAsync(It.IsAny<SearchHistory>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _paperService.SearchAsync(request, userId);

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task SearchAsync_ShouldLogSearchHistory_AfterSearch()
    {
        // Arrange
        var userId = "user-001";
        var request = new PaperSearchRequest { Query = "machine learning", Page = 1, PageSize = 10 };

        _mockPaperRepo.Setup(r => r.SearchAsync(It.IsAny<PaperSearchCriteria>()))
            .ReturnsAsync((new List<ResearchPaper>(), 0));
        _mockSearchHistoryRepo.Setup(r => r.AddAsync(It.IsAny<SearchHistory>()))
            .Returns(Task.CompletedTask);

        // Act
        await _paperService.SearchAsync(request, userId);

        // Assert: Lịch sử tìm kiếm phải được ghi lại 1 lần
        _mockSearchHistoryRepo.Verify(r => r.AddAsync(It.Is<SearchHistory>(h =>
            h.UserId == userId && h.Query == "machine learning")), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────
    // GetByIdAsync
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ShouldReturnPaperDetail_WhenPaperExists()
    {
        // Arrange
        var paperId = 1;
        var userId = "user-001";
        var paper = new ResearchPaper
        {
            Id = paperId,
            Title = "Test Paper",
            PaperTopics = [],
            PaperAuthors = [],
            PaperKeywords = [],
            PaperSources = []
        };

        _mockPaperRepo.Setup(r => r.GetPaperWithDetailsAsync(paperId)).ReturnsAsync(paper);
        _mockBookmarkRepo.Setup(r => r.GetBookmarkAsync(userId, paperId)).ReturnsAsync((Bookmark)null!);

        // Act
        var result = await _paperService.GetByIdAsync(paperId, userId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(paperId);
        result.Title.Should().Be("Test Paper");
        result.IsBookmarked.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldSetIsBookmarked_WhenUserHasBookmarked()
    {
        // Arrange
        var paperId = 1;
        var userId = "user-001";
        var paper = new ResearchPaper
        {
            Id = paperId,
            Title = "Bookmarked Paper",
            PaperTopics = [],
            PaperAuthors = [],
            PaperKeywords = [],
            PaperSources = []
        };
        var bookmark = new Bookmark { UserId = userId, PaperId = paperId };

        _mockPaperRepo.Setup(r => r.GetPaperWithDetailsAsync(paperId)).ReturnsAsync(paper);
        _mockBookmarkRepo.Setup(r => r.GetBookmarkAsync(userId, paperId)).ReturnsAsync(bookmark);

        // Act
        var result = await _paperService.GetByIdAsync(paperId, userId);

        // Assert
        result.IsBookmarked.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldThrowException_WhenPaperNotFound()
    {
        // Arrange
        var paperId = 999;
        var userId = "user-001";
        _mockPaperRepo.Setup(r => r.GetPaperWithDetailsAsync(paperId)).ReturnsAsync((ResearchPaper)null!);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _paperService.GetByIdAsync(paperId, userId));
    }

    // ─────────────────────────────────────────────────────────────
    // GetByTopicAsync
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByTopicAsync_ShouldReturnPagedResult_WhenTopicExists()
    {
        // Arrange
        var topicId = 5;
        var topic = new ResearchTopic { Id = topicId, TopicName = "Machine Learning" };
        var papers = new List<ResearchPaper>
        {
            new() { Id = 10, Title = "ML Paper 1" }
        };

        _mockTopicRepo.Setup(r => r.GetByIdAsync(topicId)).ReturnsAsync(topic);
        _mockPaperRepo.Setup(r => r.SearchAsync(It.Is<PaperSearchCriteria>(c => c.TopicId == topicId)))
            .ReturnsAsync((papers, 1));

        // Act
        var result = await _paperService.GetByTopicAsync(topicId, 1, 10);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetByTopicAsync_ShouldThrowException_WhenTopicNotFound()
    {
        // Arrange
        var topicId = 999;
        _mockTopicRepo.Setup(r => r.GetByIdAsync(topicId)).ReturnsAsync((ResearchTopic)null!);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _paperService.GetByTopicAsync(topicId, 1, 10));
    }

    // ─────────────────────────────────────────────────────────────
    // GetByJournalAsync
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByJournalAsync_ShouldReturnPagedResult_WhenJournalExists()
    {
        // Arrange
        var journalId = 3;
        var journal = new Journal { Id = journalId, Name = "Nature" };
        var papers = new List<ResearchPaper>
        {
            new() { Id = 20, Title = "Nature Paper 1" },
            new() { Id = 21, Title = "Nature Paper 2" }
        };

        _mockJournalRepo.Setup(r => r.GetByIdAsync(journalId)).ReturnsAsync(journal);
        _mockPaperRepo.Setup(r => r.SearchAsync(It.Is<PaperSearchCriteria>(c => c.JournalId == journalId)))
            .ReturnsAsync((papers, 2));

        // Act
        var result = await _paperService.GetByJournalAsync(journalId, 1, 10);

        // Assert
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetByJournalAsync_ShouldThrowException_WhenJournalNotFound()
    {
        // Arrange
        var journalId = 999;
        _mockJournalRepo.Setup(r => r.GetByIdAsync(journalId)).ReturnsAsync((Journal)null!);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _paperService.GetByJournalAsync(journalId, 1, 10));
    }

    // ─────────────────────────────────────────────────────────────
    // RecordViewAsync
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordViewAsync_ShouldIncrementViewCount_WhenPaperExists()
    {
        // Arrange
        var paperId = 1;
        var paper = new ResearchPaper { Id = paperId, Title = "Popular Paper", ViewCount = 42 };

        _mockPaperRepo.Setup(r => r.GetByIdAsync(paperId)).ReturnsAsync(paper);

        // Act
        await _paperService.RecordViewAsync(paperId);

        // Assert: ViewCount phải tăng lên 1
        paper.ViewCount.Should().Be(43);
        _mockPaperRepo.Verify(r => r.Update(paper), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task RecordViewAsync_ShouldThrowException_WhenPaperNotFound()
    {
        // Arrange
        var paperId = 999;
        _mockPaperRepo.Setup(r => r.GetByIdAsync(paperId)).ReturnsAsync((ResearchPaper)null!);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _paperService.RecordViewAsync(paperId));
    }

    // ─────────────────────────────────────────────────────────────
    // GetSearchHistoryAsync
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSearchHistoryAsync_ShouldReturnHistory_ForUser()
    {
        // Arrange
        var userId = "user-001";
        var history = new List<SearchHistory>
        {
            new() { Id = 1, UserId = userId, Query = "deep learning", SearchType = "keyword", ResultCount = 10, SearchedAt = DateTime.UtcNow.AddMinutes(-5) },
            new() { Id = 2, UserId = userId, Query = "neural network", SearchType = "keyword", ResultCount = 5, SearchedAt = DateTime.UtcNow.AddMinutes(-10) }
        };

        _mockSearchHistoryRepo.Setup(r => r.GetRecentByUserAsync(userId, 20))
            .ReturnsAsync(history);

        // Act
        var result = await _paperService.GetSearchHistoryAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.First().Query.Should().Be("deep learning");
    }

    [Fact]
    public async Task GetSearchHistoryAsync_ShouldReturnEmpty_WhenNoHistory()
    {
        // Arrange
        var userId = "new-user";
        _mockSearchHistoryRepo.Setup(r => r.GetRecentByUserAsync(userId, 20))
            .ReturnsAsync(new List<SearchHistory>());

        // Act
        var result = await _paperService.GetSearchHistoryAsync(userId);

        // Assert
        result.Should().BeEmpty();
    }
}
