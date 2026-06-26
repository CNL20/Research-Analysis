using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.DTOs.Reports;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Enums;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

public class StatisticsRepository : IStatisticsRepository
{
    private readonly ScholarTrendDbContext _context;

    public StatisticsRepository(ScholarTrendDbContext context)
    {
        _context = context;
    }

    public Task<int> CountPapersAsync(int? yearFrom = null, int? yearTo = null)
    {
        return ApplyYearFilter(_context.ResearchPapers.Where(p => p.Status == PaperStatus.Available), yearFrom, yearTo)
            .CountAsync();
    }

    public Task<int> CountKeywordsAsync() => _context.Keywords.CountAsync();

    public Task<int> CountTopicsAsync() => _context.ResearchTopics.CountAsync();

    public Task<int> CountJournalsAsync() => _context.Journals.CountAsync();

    public Task<int> CountAuthorsAsync() => _context.Authors.CountAsync();

    public Task<int> CountBookmarksAsync() => _context.Bookmarks.CountAsync();

    public async Task<int> CountFollowsAsync()
    {
        var topicFollows = await _context.FollowedTopics.CountAsync();
        var journalFollows = await _context.FollowedJournals.CountAsync();
        var authorFollows = await _context.FollowedAuthors.CountAsync();
        var paperFollows = await _context.FollowedPapers.CountAsync();
        return topicFollows + journalFollows + authorFollows + paperFollows;
    }

    public Task<int> CountActiveUsersAsync()
    {
        return _context.Users.CountAsync(u => u.IsActive);
    }

    public async Task<IReadOnlyList<ReportGroupItemDto>> GetReportByYearAsync(int? yearFrom, int? yearTo)
    {
        var query = ApplyYearFilter(
            _context.ResearchPapers.Where(p => p.Status == PaperStatus.Available && p.PublicationYear.HasValue),
            yearFrom, yearTo);

        return await query
            .GroupBy(p => p.PublicationYear!.Value)
            .Select(g => new ReportGroupItemDto
            {
                Key = g.Key.ToString(),
                PaperCount = g.Count(),
                TotalCitations = g.Sum(p => p.CitationCount ?? 0)
            })
            .OrderBy(x => x.Key)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ReportGroupItemDto>> GetReportByKeywordAsync(int? yearFrom, int? yearTo)
    {
        var query = from pk in _context.PaperKeywords
                    join p in _context.ResearchPapers on pk.PaperId equals p.Id
                    join k in _context.Keywords on pk.KeywordId equals k.Id
                    where p.Status == PaperStatus.Available
                    select new { p, k.Name };

        if (yearFrom.HasValue)
        {
            query = query.Where(x => x.p.PublicationYear >= yearFrom);
        }

        if (yearTo.HasValue)
        {
            query = query.Where(x => x.p.PublicationYear <= yearTo);
        }

        return await query
            .GroupBy(x => x.Name)
            .Select(g => new ReportGroupItemDto
            {
                Key = g.Key,
                PaperCount = g.Count(),
                TotalCitations = g.Sum(x => x.p.CitationCount ?? 0)
            })
            .OrderByDescending(x => x.PaperCount)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ReportGroupItemDto>> GetReportByTopicAsync(int? yearFrom, int? yearTo)
    {
        var query = from pt in _context.PaperTopics
                    join p in _context.ResearchPapers on pt.PaperId equals p.Id
                    join t in _context.ResearchTopics on pt.TopicId equals t.Id
                    where p.Status == PaperStatus.Available
                    select new { p, t.TopicName };

        if (yearFrom.HasValue)
        {
            query = query.Where(x => x.p.PublicationYear >= yearFrom);
        }

        if (yearTo.HasValue)
        {
            query = query.Where(x => x.p.PublicationYear <= yearTo);
        }

        return await query
            .GroupBy(x => x.TopicName)
            .Select(g => new ReportGroupItemDto
            {
                Key = g.Key,
                PaperCount = g.Count(),
                TotalCitations = g.Sum(x => x.p.CitationCount ?? 0)
            })
            .OrderByDescending(x => x.PaperCount)
            .ToListAsync();
    }

    private static IQueryable<Domain.Entities.ResearchPaper> ApplyYearFilter(
        IQueryable<Domain.Entities.ResearchPaper> query,
        int? yearFrom,
        int? yearTo)
    {
        if (yearFrom.HasValue)
        {
            query = query.Where(p => p.PublicationYear >= yearFrom);
        }

        if (yearTo.HasValue)
        {
            query = query.Where(p => p.PublicationYear <= yearTo);
        }

        return query;
    }
}
