using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.DTOs.Trends;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Application.Services.Keywords;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Domain.Enums;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

public class TrendRepository : ITrendRepository
{
    private readonly ScholarTrendDbContext _context;

    public TrendRepository(ScholarTrendDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<KeywordTrend>> GetKeywordTrendsAsync(TrendFilterCriteria criteria)
    {
        var query = ApplyKeywordPeriodFilter(
            _context.KeywordTrends.AsNoTracking().Include(t => t.Keyword),
            criteria);

        if (criteria.KeywordId.HasValue)
        {
            query = query.Where(t => t.KeywordId == criteria.KeywordId);
        }
        else
        {
            var topIds = await SelectTopKeywordIdsAsync(
                ApplyKeywordPeriodFilter(_context.KeywordTrends.AsNoTracking(), criteria),
                criteria.Top);

            if (topIds.Count == 0)
                return [];

            query = query.Where(t => topIds.Contains(t.KeywordId));
        }

        return await query
            .OrderBy(t => t.Year)
            .ThenBy(t => t.Month)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<TopicTrend>> GetTopicTrendsAsync(TrendFilterCriteria criteria)
    {
        var query = ApplyTopicPeriodFilter(
            _context.TopicTrends.AsNoTracking().Include(t => t.Topic),
            criteria);

        if (criteria.TopicId.HasValue)
        {
            query = query.Where(t => t.TopicId == criteria.TopicId);
        }
        else
        {
            var topIds = await SelectTopTopicIdsAsync(
                ApplyTopicPeriodFilter(_context.TopicTrends.AsNoTracking(), criteria),
                criteria.Top);

            if (topIds.Count == 0)
                return [];

            query = query.Where(t => topIds.Contains(t.TopicId));
        }

        return await query
            .OrderBy(t => t.Year)
            .ThenBy(t => t.Month)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<JournalTrend>> GetJournalTrendsAsync(TrendFilterCriteria criteria)
    {
        var query = ApplyJournalPeriodFilter(
            _context.JournalTrends.AsNoTracking().Include(t => t.Journal),
            criteria);

        if (criteria.JournalId.HasValue)
        {
            query = query.Where(t => t.JournalId == criteria.JournalId);
        }
        else
        {
            var topIds = await SelectTopJournalIdsAsync(
                ApplyJournalPeriodFilter(_context.JournalTrends.AsNoTracking(), criteria),
                criteria.Top);

            if (topIds.Count == 0)
                return [];

            query = query.Where(t => topIds.Contains(t.JournalId));
        }

        return await query
            .OrderBy(t => t.Year)
            .ThenBy(t => t.Month)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<TrendDataPointDto>> GetPublicationTrendAsync(TrendFilterCriteria criteria)
    {
        if (criteria.KeywordId.HasValue)
        {
            var q = ApplyKeywordPeriodFilter(
                _context.KeywordTrends.Where(t => t.KeywordId == criteria.KeywordId.Value),
                criteria);
            return await q
                .OrderBy(t => t.Year).ThenBy(t => t.Month)
                .Select(t => new TrendDataPointDto
                {
                    Year = t.Year,
                    Month = t.Month,
                    PaperCount = t.PaperCount,
                    CitationCount = t.CitationCount,
                    GrowthRate = t.GrowthRate,
                    TrendingScore = t.TrendingScore
                }).ToListAsync();
        }

        if (criteria.TopicId.HasValue)
        {
            var q = ApplyTopicPeriodFilter(
                _context.TopicTrends.Where(t => t.TopicId == criteria.TopicId.Value),
                criteria);
            return await q
                .OrderBy(t => t.Year).ThenBy(t => t.Month)
                .Select(t => new TrendDataPointDto
                {
                    Year = t.Year,
                    Month = t.Month,
                    PaperCount = t.PaperCount,
                    CitationCount = t.CitationCount,
                    GrowthRate = t.GrowthRate,
                    TrendingScore = t.TrendingScore
                }).ToListAsync();
        }

        if (criteria.JournalId.HasValue)
        {
            var q = ApplyJournalPeriodFilter(
                _context.JournalTrends.Where(t => t.JournalId == criteria.JournalId.Value),
                criteria);
            return await q
                .OrderBy(t => t.Year).ThenBy(t => t.Month)
                .Select(t => new TrendDataPointDto
                {
                    Year = t.Year,
                    Month = t.Month,
                    PaperCount = t.PaperCount,
                    CitationCount = t.CitationCount,
                    GrowthRate = t.GrowthRate,
                    TrendingScore = t.TrendingScore
                }).ToListAsync();
        }

        var query = _context.ResearchPapers
            .Where(p => PaperStatusRules.Browsable.Contains(p.Status) && p.PublicationDate.HasValue);

        if (criteria.YearFrom.HasValue)
            query = query.Where(p => p.PublicationYear >= criteria.YearFrom);

        if (criteria.YearTo.HasValue)
            query = query.Where(p => p.PublicationYear <= criteria.YearTo);

        if (criteria.MonthFrom.HasValue && criteria.YearFrom.HasValue)
        {
            var fromDate = new DateTime(criteria.YearFrom.Value, criteria.MonthFrom.Value, 1);
            query = query.Where(p => p.PublicationDate >= fromDate);
        }

        if (criteria.MonthTo.HasValue && criteria.YearTo.HasValue)
        {
            var toDate = new DateTime(criteria.YearTo.Value, criteria.MonthTo.Value, 1)
                .AddMonths(1).AddDays(-1);
            query = query.Where(p => p.PublicationDate <= toDate);
        }

        var grouped = await query
            .GroupBy(p => new
            {
                Year = p.PublicationDate!.Value.Year,
                Month = p.PublicationDate!.Value.Month
            })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                PaperCount = g.Count(),
                CitationCount = g.Sum(p => p.CitationCount ?? 0)
            })
            .OrderBy(g => g.Year)
            .ThenBy(g => g.Month)
            .ToListAsync();

        return BuildDataPointsWithGrowth(grouped.Select(g => (g.Year, g.Month, g.PaperCount, g.CitationCount)));
    }

    /// <summary>
    /// Top N entity ids inside the period-filtered window (not a single anchor month).
    /// Prefers PaperCount &gt; 0; ranks by best TrendingScore then total papers.
    /// </summary>
    private static async Task<List<int>> SelectTopKeywordIdsAsync(
        IQueryable<KeywordTrend> query,
        int top)
    {
        var take = top > 0 ? top : 10;
        var active = query.Where(t => t.PaperCount > 0);
        var source = await active.AnyAsync() ? active : query;

        return await source
            .GroupBy(t => t.KeywordId)
            .Select(g => new
            {
                Id = g.Key,
                BestScore = g.Max(t => t.TrendingScore),
                TotalPapers = g.Sum(t => t.PaperCount)
            })
            .OrderByDescending(x => x.BestScore)
            .ThenByDescending(x => x.TotalPapers)
            .Take(take)
            .Select(x => x.Id)
            .ToListAsync();
    }

    private static async Task<List<int>> SelectTopTopicIdsAsync(
        IQueryable<TopicTrend> query,
        int top)
    {
        var take = top > 0 ? top : 10;
        var active = query.Where(t => t.PaperCount > 0);
        var source = await active.AnyAsync() ? active : query;

        return await source
            .GroupBy(t => t.TopicId)
            .Select(g => new
            {
                Id = g.Key,
                BestScore = g.Max(t => t.TrendingScore),
                TotalPapers = g.Sum(t => t.PaperCount)
            })
            .OrderByDescending(x => x.BestScore)
            .ThenByDescending(x => x.TotalPapers)
            .Take(take)
            .Select(x => x.Id)
            .ToListAsync();
    }

    private static async Task<List<int>> SelectTopJournalIdsAsync(
        IQueryable<JournalTrend> query,
        int top)
    {
        var take = top > 0 ? top : 10;
        var active = query.Where(t => t.PaperCount > 0);
        var source = await active.AnyAsync() ? active : query;

        return await source
            .GroupBy(t => t.JournalId)
            .Select(g => new
            {
                Id = g.Key,
                BestScore = g.Max(t => t.TrendingScore),
                TotalPapers = g.Sum(t => t.PaperCount)
            })
            .OrderByDescending(x => x.BestScore)
            .ThenByDescending(x => x.TotalPapers)
            .Take(take)
            .Select(x => x.Id)
            .ToListAsync();
    }

    private static List<TrendDataPointDto> BuildDataPointsWithGrowth(
        IEnumerable<(int Year, int Month, int PaperCount, int CitationCount)> items)
    {
        var result = new List<TrendDataPointDto>();
        var previousCount = 0;

        foreach (var (year, month, pCount, cCount) in items.OrderBy(x => x.Year).ThenBy(x => x.Month))
        {
            var growth = KeywordTrendCalculator.CalculateGrowthRate(previousCount, pCount);
            var score = KeywordTrendCalculator.CalculateTrendingScore(pCount, growth, cCount);
            result.Add(new TrendDataPointDto
            {
                Year = year,
                Month = month,
                PaperCount = pCount,
                CitationCount = cCount,
                GrowthRate = growth,
                TrendingScore = score
            });
            previousCount = pCount;
        }

        return result;
    }

    private static IQueryable<KeywordTrend> ApplyKeywordPeriodFilter(
        IQueryable<KeywordTrend> query,
        TrendFilterCriteria criteria)
    {
        if (criteria.YearFrom.HasValue)
        {
            query = query.Where(t => t.Year > criteria.YearFrom.Value ||
                                     (t.Year == criteria.YearFrom.Value &&
                                      (!criteria.MonthFrom.HasValue || t.Month >= criteria.MonthFrom.Value)));
        }

        if (criteria.YearTo.HasValue)
        {
            query = query.Where(t => t.Year < criteria.YearTo.Value ||
                                     (t.Year == criteria.YearTo.Value &&
                                      (!criteria.MonthTo.HasValue || t.Month <= criteria.MonthTo.Value)));
        }

        return query;
    }

    private static IQueryable<TopicTrend> ApplyTopicPeriodFilter(
        IQueryable<TopicTrend> query,
        TrendFilterCriteria criteria)
    {
        if (criteria.YearFrom.HasValue)
        {
            query = query.Where(t => t.Year > criteria.YearFrom.Value ||
                                     (t.Year == criteria.YearFrom.Value &&
                                      (!criteria.MonthFrom.HasValue || t.Month >= criteria.MonthFrom.Value)));
        }

        if (criteria.YearTo.HasValue)
        {
            query = query.Where(t => t.Year < criteria.YearTo.Value ||
                                     (t.Year == criteria.YearTo.Value &&
                                      (!criteria.MonthTo.HasValue || t.Month <= criteria.MonthTo.Value)));
        }

        return query;
    }

    private static IQueryable<JournalTrend> ApplyJournalPeriodFilter(
        IQueryable<JournalTrend> query,
        TrendFilterCriteria criteria)
    {
        if (criteria.YearFrom.HasValue)
        {
            query = query.Where(t => t.Year > criteria.YearFrom.Value ||
                                     (t.Year == criteria.YearFrom.Value &&
                                      (!criteria.MonthFrom.HasValue || t.Month >= criteria.MonthFrom.Value)));
        }

        if (criteria.YearTo.HasValue)
        {
            query = query.Where(t => t.Year < criteria.YearTo.Value ||
                                     (t.Year == criteria.YearTo.Value &&
                                      (!criteria.MonthTo.HasValue || t.Month <= criteria.MonthTo.Value)));
        }

        return query;
    }
}
