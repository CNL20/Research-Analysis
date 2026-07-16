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

    public Task<FollowedAuthor?> GetFollowedAuthorAsync(string userId, int authorId)
    {
        return _context.FollowedAuthors
            .FirstOrDefaultAsync(f => f.UserId == userId && f.AuthorId == authorId);
    }

    public Task<FollowedPaper?> GetFollowedPaperAsync(string userId, int paperId)
    {
        return _context.FollowedPapers
            .FirstOrDefaultAsync(f => f.UserId == userId && f.PaperId == paperId);
    }

    public async Task<(IReadOnlyList<FollowedTopic> Items, int TotalCount)> GetUserFollowedTopicsAsync(string userId, int page = 1, int pageSize = 10)
    {
        var query = _context.FollowedTopics.Where(f => f.UserId == userId);
        var totalCount = await query.CountAsync();

        var items = await query
            .Include(f => f.Topic)
            .OrderByDescending(f => f.FollowedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<FollowedJournal> Items, int TotalCount)> GetUserFollowedJournalsAsync(string userId, int page = 1, int pageSize = 10)
    {
        var query = _context.FollowedJournals.Where(f => f.UserId == userId);
        var totalCount = await query.CountAsync();

        var items = await query
            .Include(f => f.Journal)
            .OrderByDescending(f => f.FollowedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<FollowedAuthor> Items, int TotalCount)> GetUserFollowedAuthorsAsync(string userId, int page = 1, int pageSize = 10)
    {
        var query = _context.FollowedAuthors.Where(f => f.UserId == userId);
        var totalCount = await query.CountAsync();

        var items = await query
            .Include(f => f.Author)
            .OrderByDescending(f => f.FollowedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<FollowedPaper> Items, int TotalCount)> GetUserFollowedPapersAsync(string userId, int page = 1, int pageSize = 10)
    {
        var query = _context.FollowedPapers.Where(f => f.UserId == userId);
        var totalCount = await query.CountAsync();

        var items = await query
            .Include(f => f.Paper)
            .OrderByDescending(f => f.FollowedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task AddTopicAsync(FollowedTopic follow)
    {
        await _context.FollowedTopics.AddAsync(follow);
    }

    public async Task AddJournalAsync(FollowedJournal follow)
    {
        await _context.FollowedJournals.AddAsync(follow);
    }

    public async Task AddAuthorAsync(FollowedAuthor follow)
    {
        await _context.FollowedAuthors.AddAsync(follow);
    }

    public async Task AddPaperAsync(FollowedPaper follow)
    {
        await _context.FollowedPapers.AddAsync(follow);
    }

    public void RemoveTopic(FollowedTopic follow)
    {
        _context.FollowedTopics.Remove(follow);
    }

    public void RemoveJournal(FollowedJournal follow)
    {
        _context.FollowedJournals.Remove(follow);
    }

    public void RemoveAuthor(FollowedAuthor follow)
    {
        _context.FollowedAuthors.Remove(follow);
    }

    public void RemovePaper(FollowedPaper follow)
    {
        _context.FollowedPapers.Remove(follow);
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

    public async Task<IReadOnlyList<string>> GetAuthorFollowerUserIdsAsync(int authorId)
    {
        return await _context.FollowedAuthors
            .Where(f => f.AuthorId == authorId)
            .Select(f => f.UserId)
            .Distinct()
            .ToListAsync();
    }
}
