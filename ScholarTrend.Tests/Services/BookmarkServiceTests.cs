using FluentAssertions;
using Moq;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Application.Services;
using ScholarTrend.Domain.Entities;
using Xunit;

namespace ScholarTrend.Tests.Services;

public class BookmarkServiceTests
{
    private readonly Mock<IBookmarkRepository> _mockBookmarkRepo;
    private readonly Mock<IResearchPaperRepository> _mockPaperRepo;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly BookmarkService _bookmarkService;

    public BookmarkServiceTests()
    {
        _mockBookmarkRepo = new Mock<IBookmarkRepository>();
        _mockPaperRepo = new Mock<IResearchPaperRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        
        _mockUnitOfWork.Setup(u => u.Bookmarks).Returns(_mockBookmarkRepo.Object);
        _mockUnitOfWork.Setup(u => u.ResearchPapers).Returns(_mockPaperRepo.Object);

        _bookmarkService = new BookmarkService(_mockUnitOfWork.Object);
    }

    [Fact]
    public async Task AddBookmarkAsync_ShouldThrowException_WhenPaperNotFound()
    {
        var userId = "user-123";
        var paperId = 999;
        _mockPaperRepo.Setup(x => x.GetByIdAsync(paperId)).ReturnsAsync((ResearchPaper)null!);

        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _bookmarkService.AddBookmarkAsync(userId, paperId));
    }

    [Fact]
    public async Task AddBookmarkAsync_ShouldThrowException_WhenAlreadyBookmarked()
    {
        var userId = "user-123";
        var paperId = 1;
        var existingBookmark = new Bookmark { UserId = userId, PaperId = paperId };
        
        _mockPaperRepo.Setup(x => x.GetByIdAsync(paperId)).ReturnsAsync(new ResearchPaper { Id = paperId });
        _mockBookmarkRepo.Setup(x => x.GetBookmarkAsync(userId, paperId)).ReturnsAsync(existingBookmark);

        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _bookmarkService.AddBookmarkAsync(userId, paperId));
    }

    [Fact]
    public async Task AddBookmarkAsync_ShouldAddAndSave_WhenValid()
    {
        var userId = "user-123";
        var paperId = 1;
        
        _mockPaperRepo.Setup(x => x.GetByIdAsync(paperId)).ReturnsAsync(new ResearchPaper { Id = paperId });
        _mockBookmarkRepo.Setup(x => x.GetBookmarkAsync(userId, paperId)).ReturnsAsync((Bookmark)null!);

        await _bookmarkService.AddBookmarkAsync(userId, paperId);

        _mockBookmarkRepo.Verify(x => x.AddAsync(It.IsAny<Bookmark>()), Times.Once);
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task RemoveBookmarkAsync_ShouldThrowException_WhenBookmarkNotFound()
    {
        var userId = "user-123";
        var paperId = 1;
        _mockBookmarkRepo.Setup(x => x.GetBookmarkAsync(userId, paperId)).ReturnsAsync((Bookmark)null!);

        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _bookmarkService.RemoveBookmarkAsync(userId, paperId));
    }

    [Fact]
    public async Task RemoveBookmarkAsync_ShouldRemoveAndSave_WhenValid()
    {
        var userId = "user-123";
        var paperId = 1;
        var existingBookmark = new Bookmark { UserId = userId, PaperId = paperId };
        
        _mockBookmarkRepo.Setup(x => x.GetBookmarkAsync(userId, paperId)).ReturnsAsync(existingBookmark);

        await _bookmarkService.RemoveBookmarkAsync(userId, paperId);

        _mockBookmarkRepo.Verify(x => x.Delete(existingBookmark), Times.Once);
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetUserBookmarksAsync_ShouldReturnPagedResult()
    {
        var userId = "user-123";
        var bookmarks = new List<Bookmark>
        {
            new Bookmark { Paper = new ResearchPaper { Id = 1, Title = "Paper 1" } },
            new Bookmark { Paper = new ResearchPaper { Id = 2, Title = "Paper 2" } }
        };
        
        _mockBookmarkRepo.Setup(x => x.GetUserBookmarksAsync(userId, It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync((bookmarks, 2));

        var result = await _bookmarkService.GetUserBookmarksAsync(userId, 1, 10);

        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Items.First().Title.Should().Be("Paper 1");
    }
}
