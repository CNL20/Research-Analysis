using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

public class FollowRepository : IFollowRepository
{
    private readonly ScholarTrendDbContext _context;

    public FollowRepository(ScholarTrendDbContext context)
    {
        _context = context;
    }

    public Task<FollowedTopic?> GetFollowedTopicAsync(string userId, int topicId)
    {
        return _context.FollowedTopics
            .FirstOrDefaultAsync(f => f.UserId == userId && f.TopicId == topicId);
    }

    public Task<FollowedJournal?> GetFollowedJournalAsync(string userId, int journalId)
    {
        return _context.FollowedJournals
            .FirstOrDefaultAsync(f => f.UserId == userId && f.JournalId == journalId);
    }

    public async Task<IReadOnlyList<FollowedTopic>> GetUserFollowedTopicsAsync(string userId)
    {
        return await _context.FollowedTopics
            .Include(f => f.Topic)
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.FollowedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<FollowedJournal>> GetUserFollowedJournalsAsync(string userId)
    {
        return await _context.FollowedJournals
            .Include(f => f.Journal)
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.FollowedAt)
            .ToListAsync();
    }

    public async Task AddTopicAsync(FollowedTopic follow)
    {
        await _context.FollowedTopics.AddAsync(follow);
    }

    public async Task AddJournalAsync(FollowedJournal follow)
    {
        await _context.FollowedJournals.AddAsync(follow);
    }

    public void RemoveTopic(FollowedTopic follow)
    {
        _context.FollowedTopics.Remove(follow);
    }

    public void RemoveJournal(FollowedJournal follow)
    {
        _context.FollowedJournals.Remove(follow);
    }

    public async Task<IReadOnlyList<string>> GetTopicFollowerUserIdsAsync(int topicId)
    {
        return await _context.FollowedTopics
            .Where(f => f.TopicId == topicId)
            .Select(f => f.UserId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<IReadOnlyList<string>> GetJournalFollowerUserIdsAsync(int journalId)
    {
        return await _context.FollowedJournals
            .Where(f => f.JournalId == journalId)
            .Select(f => f.UserId)
            .Distinct()
            .ToListAsync();
    }
}
