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
        return ApplyYearFilter(_context.ResearchPapers.Where(p => PaperStatusRules.Browsable.Contains(p.Status)), yearFrom, yearTo)
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
            _context.ResearchPapers.Where(p => PaperStatusRules.Browsable.Contains(p.Status) && p.PublicationYear.HasValue),
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
                    where PaperStatusRules.Browsable.Contains(p.Status)
                    select new { p, KeywordId = k.Id, k.Name };

        if (yearFrom.HasValue)
            query = query.Where(x => x.p.PublicationYear >= yearFrom);
        if (yearTo.HasValue)
            query = query.Where(x => x.p.PublicationYear <= yearTo);

        return await query
            .GroupBy(x => new { x.KeywordId, x.Name })
            .Select(g => new ReportGroupItemDto
            {
                Id = g.Key.KeywordId,
                Key = g.Key.Name,
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
                    where PaperStatusRules.Browsable.Contains(p.Status)
                    select new { p, TopicId = t.Id, Name = t.TopicName };

        if (yearFrom.HasValue)
            query = query.Where(x => x.p.PublicationYear >= yearFrom);
        if (yearTo.HasValue)
            query = query.Where(x => x.p.PublicationYear <= yearTo);

        return await query
            .GroupBy(x => new { x.TopicId, x.Name })
            .Select(g => new ReportGroupItemDto
            {
                Id = g.Key.TopicId,
                Key = g.Key.Name,
                PaperCount = g.Count(),
                TotalCitations = g.Sum(x => x.p.CitationCount ?? 0)
            })
            .OrderByDescending(x => x.PaperCount)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ReportGroupItemDto>> GetReportByJournalAsync(int? yearFrom, int? yearTo)
    {
        var query = ApplyYearFilter(
            _context.ResearchPapers.Where(p =>
                PaperStatusRules.Browsable.Contains(p.Status) && p.JournalId.HasValue),
            yearFrom,
            yearTo);

        return await query
            .GroupBy(p => new { JournalId = p.JournalId!.Value, Name = p.Journal!.Name })
            .Select(g => new ReportGroupItemDto
            {
                Id = g.Key.JournalId,
                Key = g.Key.Name,
                PaperCount = g.Count(),
                TotalCitations = g.Sum(p => p.CitationCount ?? 0)
            })
            .OrderByDescending(x => x.PaperCount)
            .ToListAsync();
    }

    public async Task<IReadOnlyDictionary<int, double>> GetKeywordReliabilityAsync(
        IEnumerable<int> keywordIds, int? yearFrom, int? yearTo)
    {
        var ids = keywordIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, double>();

        var query = from pk in _context.PaperKeywords
                    join p in _context.ResearchPapers on pk.PaperId equals p.Id
                    where ids.Contains(pk.KeywordId) && PaperStatusRules.Browsable.Contains(p.Status)
                    select new { pk.KeywordId, p.Doi, p.Abstract, p.JournalId, p.PublicationYear };

        if (yearFrom.HasValue)
            query = query.Where(x => x.PublicationYear >= yearFrom);
        if (yearTo.HasValue)
            query = query.Where(x => x.PublicationYear <= yearTo);

        var rows = await query.ToListAsync();
        return rows
            .GroupBy(x => x.KeywordId)
            .ToDictionary(
                g => g.Key,
                g => AverageReliability(g.Select(x => ((string?)x.Doi, (string?)x.Abstract, x.JournalId))));
    }

    public async Task<IReadOnlyDictionary<int, double>> GetTopicReliabilityAsync(
        IEnumerable<int> topicIds, int? yearFrom, int? yearTo)
    {
        var ids = topicIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, double>();

        var query = from pt in _context.PaperTopics
                    join p in _context.ResearchPapers on pt.PaperId equals p.Id
                    where ids.Contains(pt.TopicId) && PaperStatusRules.Browsable.Contains(p.Status)
                    select new { pt.TopicId, p.Doi, p.Abstract, p.JournalId, p.PublicationYear };

        if (yearFrom.HasValue)
            query = query.Where(x => x.PublicationYear >= yearFrom);
        if (yearTo.HasValue)
            query = query.Where(x => x.PublicationYear <= yearTo);

        var rows = await query.ToListAsync();
        return rows
            .GroupBy(x => x.TopicId)
            .ToDictionary(
                g => g.Key,
                g => AverageReliability(g.Select(x => ((string?)x.Doi, (string?)x.Abstract, x.JournalId))));
    }

    public async Task<IReadOnlyDictionary<int, double>> GetJournalReliabilityAsync(
        IEnumerable<int> journalIds, int? yearFrom, int? yearTo)
    {
        var ids = journalIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, double>();

        var query = ApplyYearFilter(
            _context.ResearchPapers.Where(p =>
                PaperStatusRules.Browsable.Contains(p.Status)
                && p.JournalId.HasValue
                && ids.Contains(p.JournalId.Value)),
            yearFrom,
            yearTo);

        var rows = await query
            .Select(p => new { Id = p.JournalId!.Value, p.Doi, p.Abstract, Jid = p.JournalId })
            .ToListAsync();

        return rows
            .GroupBy(x => x.Id)
            .ToDictionary(
                g => g.Key,
                g => AverageReliability(g.Select(x => ((string?)x.Doi, (string?)x.Abstract, x.Jid))));
    }

    private static double AverageReliability(
        IEnumerable<(string? Doi, string? Abstract, int? JournalId)> papers)
    {
        var list = papers.ToList();
        if (list.Count == 0) return 0;

        var sum = list.Sum(p =>
        {
            var score = 0.0;
            if (!string.IsNullOrWhiteSpace(p.Doi)) score += 1;
            if (!string.IsNullOrWhiteSpace(p.Abstract)) score += 1;
            if (p.JournalId.HasValue) score += 1;
            return score / 3.0 * 100.0;
        });

        return Math.Round(sum / list.Count, 1);
    }

    private static IQueryable<Domain.Entities.ResearchPaper> ApplyYearFilter(
        IQueryable<Domain.Entities.ResearchPaper> query,
        int? yearFrom,
        int? yearTo)
    {
        if (yearFrom.HasValue)
            query = query.Where(p => p.PublicationYear >= yearFrom);

        if (yearTo.HasValue)
            query = query.Where(p => p.PublicationYear <= yearTo);

        return query;
    }
}
