using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface IFollowRepository
{
    Task<FollowedTopic?> GetFollowedTopicAsync(string userId, int topicId);
    Task<FollowedJournal?> GetFollowedJournalAsync(string userId, int journalId);
    Task<FollowedAuthor?> GetFollowedAuthorAsync(string userId, int authorId);
    Task<FollowedPaper?> GetFollowedPaperAsync(string userId, int paperId);
    Task<(IReadOnlyList<FollowedTopic> Items, int TotalCount)> GetUserFollowedTopicsAsync(string userId, int page = 1, int pageSize = 10);
    Task<(IReadOnlyList<FollowedJournal> Items, int TotalCount)> GetUserFollowedJournalsAsync(string userId, int page = 1, int pageSize = 10);
    Task<(IReadOnlyList<FollowedAuthor> Items, int TotalCount)> GetUserFollowedAuthorsAsync(string userId, int page = 1, int pageSize = 10);
    Task<(IReadOnlyList<FollowedPaper> Items, int TotalCount)> GetUserFollowedPapersAsync(string userId, int page = 1, int pageSize = 10);
    Task AddTopicAsync(FollowedTopic follow);
    Task AddJournalAsync(FollowedJournal follow);
    Task AddAuthorAsync(FollowedAuthor follow);
    Task AddPaperAsync(FollowedPaper follow);
    void RemoveTopic(FollowedTopic follow);
    void RemoveJournal(FollowedJournal follow);
    void RemoveAuthor(FollowedAuthor follow);
    void RemovePaper(FollowedPaper follow);
    Task<IReadOnlyList<string>> GetTopicFollowerUserIdsAsync(int topicId);
    Task<IReadOnlyList<string>> GetJournalFollowerUserIdsAsync(int journalId);
    Task<IReadOnlyList<string>> GetAuthorFollowerUserIdsAsync(int authorId);
}
