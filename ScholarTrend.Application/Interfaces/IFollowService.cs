using ScholarTrend.Application.DTOs.Follows;

namespace ScholarTrend.Application.Interfaces;

public interface IFollowService
{
    Task<IReadOnlyList<FollowItemDto>> GetFollowedTopicsAsync(string userId);
    Task<IReadOnlyList<FollowItemDto>> GetFollowedJournalsAsync(string userId);
    Task<IReadOnlyList<FollowItemDto>> GetFollowedAuthorsAsync(string userId);
    Task<IReadOnlyList<FollowItemDto>> GetFollowedPapersAsync(string userId);
    Task<FollowItemDto> FollowTopicAsync(string userId, int topicId);
    Task UnfollowTopicAsync(string userId, int topicId);
    Task<FollowItemDto> FollowJournalAsync(string userId, int journalId);
    Task UnfollowJournalAsync(string userId, int journalId);
    Task<FollowItemDto> FollowAuthorAsync(string userId, int authorId);
    Task UnfollowAuthorAsync(string userId, int authorId);
    Task<FollowItemDto> FollowPaperAsync(string userId, int paperId);
    Task UnfollowPaperAsync(string userId, int paperId);
}
