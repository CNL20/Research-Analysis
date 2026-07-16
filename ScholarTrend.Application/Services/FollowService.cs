using ScholarTrend.Application.DTOs.Follows;
using ScholarTrend.Application.DTOs.Common;
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

    public async Task<PagedResult<FollowItemDto>> GetFollowedTopicsAsync(string userId, int page = 1, int pageSize = 10)
    {
        var (follows, totalCount) = await _unitOfWork.Follows.GetUserFollowedTopicsAsync(userId, page, pageSize);
        var items = follows.Select(f => new FollowItemDto
        {
            Id = f.Id,
            TargetId = f.TopicId,
            Name = f.Topic.TopicName,
            Type = "topic",
            FollowedAt = f.FollowedAt
        }).ToList();

        return new PagedResult<FollowItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<FollowItemDto>> GetFollowedJournalsAsync(string userId, int page = 1, int pageSize = 10)
    {
        var (follows, totalCount) = await _unitOfWork.Follows.GetUserFollowedJournalsAsync(userId, page, pageSize);
        var items = follows.Select(f => new FollowItemDto
        {
            Id = f.Id,
            TargetId = f.JournalId,
            Name = f.Journal.Name,
            Type = "journal",
            FollowedAt = f.FollowedAt
        }).ToList();

        return new PagedResult<FollowItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
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

    public async Task<PagedResult<FollowItemDto>> GetFollowedAuthorsAsync(string userId, int page = 1, int pageSize = 10)
    {
        var (follows, totalCount) = await _unitOfWork.Follows.GetUserFollowedAuthorsAsync(userId, page, pageSize);
        var items = follows.Select(f => new FollowItemDto
        {
            Id = f.Id,
            TargetId = f.AuthorId,
            Name = f.Author.Name,
            Type = "author",
            FollowedAt = f.FollowedAt
        }).ToList();

        return new PagedResult<FollowItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<FollowItemDto>> GetFollowedPapersAsync(string userId, int page = 1, int pageSize = 10)
    {
        var (follows, totalCount) = await _unitOfWork.Follows.GetUserFollowedPapersAsync(userId, page, pageSize);
        var items = follows.Select(f => new FollowItemDto
        {
            Id = f.Id,
            TargetId = f.PaperId,
            Name = f.Paper.Title,
            Type = "paper",
            FollowedAt = f.FollowedAt
        }).ToList();

        return new PagedResult<FollowItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<FollowCountsDto> GetFollowCountsAsync(string userId)
    {
        var topics = await _unitOfWork.Follows.GetUserFollowedTopicsAsync(userId, 1, 1);
        var authors = await _unitOfWork.Follows.GetUserFollowedAuthorsAsync(userId, 1, 1);
        var journals = await _unitOfWork.Follows.GetUserFollowedJournalsAsync(userId, 1, 1);
        var papers = await _unitOfWork.Follows.GetUserFollowedPapersAsync(userId, 1, 1);

        return new FollowCountsDto
        {
            TopicsCount = topics.TotalCount,
            AuthorsCount = authors.TotalCount,
            JournalsCount = journals.TotalCount,
            PapersCount = papers.TotalCount
        };
    }

    public async Task<FollowItemDto> FollowAuthorAsync(string userId, int authorId)
    {
        var author = await _unitOfWork.Authors.GetByIdAsync(authorId);
        if (author == null)
        {
            throw new InvalidOperationException("Author not found.");
        }

        var existing = await _unitOfWork.Follows.GetFollowedAuthorAsync(userId, authorId);
        if (existing != null)
        {
            throw new InvalidOperationException("Author is already followed.");
        }

        var follow = new FollowedAuthor
        {
            UserId = userId,
            AuthorId = authorId,
            FollowedAt = DateTime.UtcNow
        };

        await _unitOfWork.Follows.AddAuthorAsync(follow);
        await _unitOfWork.SaveChangesAsync();

        return new FollowItemDto
        {
            Id = follow.Id,
            TargetId = authorId,
            Name = author.Name,
            Type = "author",
            FollowedAt = follow.FollowedAt
        };
    }

    public async Task UnfollowAuthorAsync(string userId, int authorId)
    {
        var follow = await _unitOfWork.Follows.GetFollowedAuthorAsync(userId, authorId);
        if (follow == null)
        {
            throw new InvalidOperationException("Author is not followed.");
        }

        _unitOfWork.Follows.RemoveAuthor(follow);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<FollowItemDto> FollowPaperAsync(string userId, int paperId)
    {
        var paper = await _unitOfWork.ResearchPapers.GetByIdAsync(paperId);
        if (paper == null)
        {
            throw new InvalidOperationException("Paper not found.");
        }

        var existing = await _unitOfWork.Follows.GetFollowedPaperAsync(userId, paperId);
        if (existing != null)
        {
            throw new InvalidOperationException("Paper is already followed.");
        }

        var follow = new FollowedPaper
        {
            UserId = userId,
            PaperId = paperId,
            FollowedAt = DateTime.UtcNow
        };

        await _unitOfWork.Follows.AddPaperAsync(follow);
        await _unitOfWork.SaveChangesAsync();

        return new FollowItemDto
        {
            Id = follow.Id,
            TargetId = paperId,
            Name = paper.Title,
            Type = "paper",
            FollowedAt = follow.FollowedAt
        };
    }

    public async Task UnfollowPaperAsync(string userId, int paperId)
    {
        var follow = await _unitOfWork.Follows.GetFollowedPaperAsync(userId, paperId);
        if (follow == null)
        {
            throw new InvalidOperationException("Paper is not followed.");
        }

        _unitOfWork.Follows.RemovePaper(follow);
        await _unitOfWork.SaveChangesAsync();
    }
}
