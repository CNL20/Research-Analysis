using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface IFollowRepository
{
    Task<FollowedTopic?> GetFollowedTopicAsync(string userId, int topicId);
    Task<FollowedJournal?> GetFollowedJournalAsync(string userId, int journalId);
    Task<IReadOnlyList<FollowedTopic>> GetUserFollowedTopicsAsync(string userId);
    Task<IReadOnlyList<FollowedJournal>> GetUserFollowedJournalsAsync(string userId);
    Task AddTopicAsync(FollowedTopic follow);
    Task AddJournalAsync(FollowedJournal follow);
    void RemoveTopic(FollowedTopic follow);
    void RemoveJournal(FollowedJournal follow);
    Task<IReadOnlyList<string>> GetTopicFollowerUserIdsAsync(int topicId);
    Task<IReadOnlyList<string>> GetJournalFollowerUserIdsAsync(int journalId);
}
