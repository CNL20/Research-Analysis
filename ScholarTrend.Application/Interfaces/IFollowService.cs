using ScholarTrend.Application.DTOs.Follows;
using ScholarTrend.Application.DTOs.Common;

namespace ScholarTrend.Application.Interfaces;

public interface IFollowService
{
    Task<PagedResult<FollowItemDto>> GetFollowedTopicsAsync(string userId, int page = 1, int pageSize = 10);
    Task<PagedResult<FollowItemDto>> GetFollowedJournalsAsync(string userId, int page = 1, int pageSize = 10);
    Task<PagedResult<FollowItemDto>> GetFollowedAuthorsAsync(string userId, int page = 1, int pageSize = 10);
    Task<PagedResult<FollowItemDto>> GetFollowedPapersAsync(string userId, int page = 1, int pageSize = 10);
    Task<FollowCountsDto> GetFollowCountsAsync(string userId);
    Task<FollowItemDto> FollowTopicAsync(string userId, int topicId);
    Task UnfollowTopicAsync(string userId, int topicId);
    Task<FollowItemDto> FollowJournalAsync(string userId, int journalId);
    Task UnfollowJournalAsync(string userId, int journalId);
    Task<FollowItemDto> FollowAuthorAsync(string userId, int authorId);
    Task UnfollowAuthorAsync(string userId, int authorId);
    Task<FollowItemDto> FollowPaperAsync(string userId, int paperId);
    Task UnfollowPaperAsync(string userId, int paperId);
}
