using ScholarTrend.Application.DTOs.Follows;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Services;

public class FollowService : IFollowService
{
    private readonly IUnitOfWork _unitOfWork;

    public FollowService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<FollowItemDto>> GetFollowedTopicsAsync(string userId)
    {
        var follows = await _unitOfWork.Follows.GetUserFollowedTopicsAsync(userId);
        return follows.Select(f => new FollowItemDto
        {
            Id = f.Id,
            TargetId = f.TopicId,
            Name = f.Topic.TopicName,
            Type = "topic",
            FollowedAt = f.FollowedAt
        }).ToList();
    }

    public async Task<IReadOnlyList<FollowItemDto>> GetFollowedJournalsAsync(string userId)
    {
        var follows = await _unitOfWork.Follows.GetUserFollowedJournalsAsync(userId);
        return follows.Select(f => new FollowItemDto
        {
            Id = f.Id,
            TargetId = f.JournalId,
            Name = f.Journal.Name,
            Type = "journal",
            FollowedAt = f.FollowedAt
        }).ToList();
    }

    public async Task<FollowItemDto> FollowTopicAsync(string userId, int topicId)
    {
        var topic = await _unitOfWork.Topics.GetByIdAsync(topicId);
        if (topic == null)
        {
            throw new InvalidOperationException("Topic not found.");
        }

        var existing = await _unitOfWork.Follows.GetFollowedTopicAsync(userId, topicId);
        if (existing != null)
        {
            throw new InvalidOperationException("Topic is already followed.");
        }

        var follow = new FollowedTopic
        {
            UserId = userId,
            TopicId = topicId,
            FollowedAt = DateTime.UtcNow
        };

        await _unitOfWork.Follows.AddTopicAsync(follow);
        await _unitOfWork.SaveChangesAsync();

        return new FollowItemDto
        {
            Id = follow.Id,
            TargetId = topicId,
            Name = topic.TopicName,
            Type = "topic",
            FollowedAt = follow.FollowedAt
        };
    }

    public async Task UnfollowTopicAsync(string userId, int topicId)
    {
        var follow = await _unitOfWork.Follows.GetFollowedTopicAsync(userId, topicId);
        if (follow == null)
        {
            throw new InvalidOperationException("Topic is not followed.");
        }

        _unitOfWork.Follows.RemoveTopic(follow);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<FollowItemDto> FollowJournalAsync(string userId, int journalId)
    {
        var journal = await _unitOfWork.Journals.GetByIdAsync(journalId);
        if (journal == null)
        {
            throw new InvalidOperationException("Journal not found.");
        }

        var existing = await _unitOfWork.Follows.GetFollowedJournalAsync(userId, journalId);
        if (existing != null)
        {
            throw new InvalidOperationException("Journal is already followed.");
        }

        var follow = new FollowedJournal
        {
            UserId = userId,
            JournalId = journalId,
            FollowedAt = DateTime.UtcNow
        };

        await _unitOfWork.Follows.AddJournalAsync(follow);
        await _unitOfWork.SaveChangesAsync();

        return new FollowItemDto
        {
            Id = follow.Id,
            TargetId = journalId,
            Name = journal.Name,
            Type = "journal",
            FollowedAt = follow.FollowedAt
        };
    }

    public async Task UnfollowJournalAsync(string userId, int journalId)
    {
        var follow = await _unitOfWork.Follows.GetFollowedJournalAsync(userId, journalId);
        if (follow == null)
        {
            throw new InvalidOperationException("Journal is not followed.");
        }

        _unitOfWork.Follows.RemoveJournal(follow);
        await _unitOfWork.SaveChangesAsync();
    }
}
