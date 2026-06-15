using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface IFollowRepository
{
    Task<FollowedTopic?> GetFollowedTopicAsync(string userId, int topicId);
    Task<FollowedJournal?> GetFollowedJournalAsync(string userId, int journalId);
    Task<FollowedAuthor?> GetFollowedAuthorAsync(string userId, int authorId);
    Task<FollowedPaper?> GetFollowedPaperAsync(string userId, int paperId);
    Task<IReadOnlyList<FollowedTopic>> GetUserFollowedTopicsAsync(string userId);
    Task<IReadOnlyList<FollowedJournal>> GetUserFollowedJournalsAsync(string userId);
    Task<IReadOnlyList<FollowedAuthor>> GetUserFollowedAuthorsAsync(string userId);
    Task<IReadOnlyList<FollowedPaper>> GetUserFollowedPapersAsync(string userId);
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
    Task<Author?> GetAuthorByIdAsync(int authorId);
}
