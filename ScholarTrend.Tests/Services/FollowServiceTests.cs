using FluentAssertions;
using Moq;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Application.Services;
using ScholarTrend.Domain.Entities;
using Xunit;

namespace ScholarTrend.Tests.Services;

public class FollowServiceTests
{
    private readonly Mock<IFollowRepository> _mockFollowRepo;
    private readonly Mock<IResearchTopicRepository> _mockTopicRepo;
    private readonly Mock<IJournalRepository> _mockJournalRepo;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly FollowService _followService;

    public FollowServiceTests()
    {
        _mockFollowRepo = new Mock<IFollowRepository>();
        _mockTopicRepo = new Mock<IResearchTopicRepository>();
        _mockJournalRepo = new Mock<IJournalRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        
        _mockUnitOfWork.Setup(u => u.Follows).Returns(_mockFollowRepo.Object);
        _mockUnitOfWork.Setup(u => u.Topics).Returns(_mockTopicRepo.Object);
        _mockUnitOfWork.Setup(u => u.Journals).Returns(_mockJournalRepo.Object);

        _followService = new FollowService(_mockUnitOfWork.Object);
    }

    [Fact]
    public async Task FollowTopicAsync_ShouldThrowException_WhenTopicNotFound()
    {
        var userId = "user-123";
        var topicId = 99;
        _mockTopicRepo.Setup(x => x.GetByIdAsync(topicId)).ReturnsAsync((ResearchTopic)null!);

        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _followService.FollowTopicAsync(userId, topicId));
    }

    [Fact]
    public async Task FollowTopicAsync_ShouldThrowException_WhenAlreadyFollowed()
    {
        var userId = "user-123";
        var topicId = 1;
        _mockTopicRepo.Setup(x => x.GetByIdAsync(topicId)).ReturnsAsync(new ResearchTopic { Id = topicId });
        _mockFollowRepo.Setup(x => x.GetFollowedTopicAsync(userId, topicId)).ReturnsAsync(new FollowedTopic { UserId = userId, TopicId = topicId });

        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _followService.FollowTopicAsync(userId, topicId));
    }

    [Fact]
    public async Task FollowTopicAsync_ShouldAddAndSave_WhenValid()
    {
        var userId = "user-123";
        var topicId = 1;
        _mockTopicRepo.Setup(x => x.GetByIdAsync(topicId)).ReturnsAsync(new ResearchTopic { Id = topicId });
        _mockFollowRepo.Setup(x => x.GetFollowedTopicAsync(userId, topicId)).ReturnsAsync((FollowedTopic)null!);

        await _followService.FollowTopicAsync(userId, topicId);

        _mockFollowRepo.Verify(x => x.AddTopicAsync(It.IsAny<FollowedTopic>()), Times.Once);
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UnfollowTopicAsync_ShouldThrowException_WhenNotFollowing()
    {
        var userId = "user-123";
        var topicId = 1;
        _mockFollowRepo.Setup(x => x.GetFollowedTopicAsync(userId, topicId)).ReturnsAsync((FollowedTopic)null!);

        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _followService.UnfollowTopicAsync(userId, topicId));
    }

    [Fact]
    public async Task FollowJournalAsync_ShouldThrowException_WhenJournalNotFound()
    {
        var userId = "user-123";
        var journalId = 99;
        _mockJournalRepo.Setup(x => x.GetByIdAsync(journalId)).ReturnsAsync((Journal)null!);

        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _followService.FollowJournalAsync(userId, journalId));
    }

    [Fact]
    public async Task UnfollowJournalAsync_ShouldRemoveAndSave_WhenValid()
    {
        var userId = "user-123";
        var journalId = 1;
        var existingFollow = new FollowedJournal { UserId = userId, JournalId = journalId };
        
        _mockFollowRepo.Setup(x => x.GetFollowedJournalAsync(userId, journalId)).ReturnsAsync(existingFollow);

        await _followService.UnfollowJournalAsync(userId, journalId);

        _mockFollowRepo.Verify(x => x.RemoveJournal(existingFollow), Times.Once);
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(), Times.Once);
    }
}
