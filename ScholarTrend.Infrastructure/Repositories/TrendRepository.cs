using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.DTOs.Trends;
using ScholarTrend.Application.Interfaces.Repositories;
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
        var query = _context.KeywordTrends
            .Include(t => t.Keyword)
            .AsQueryable();

        if (criteria.KeywordId.HasValue)
        {
            query = query.Where(t => t.KeywordId == criteria.KeywordId);
        }

        query = ApplyKeywordPeriodFilter(query, criteria);

        return await query
            .OrderBy(t => t.Year)
            .ThenBy(t => t.Month)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<TopicTrend>> GetTopicTrendsAsync(TrendFilterCriteria criteria)
    {
        var query = _context.TopicTrends
            .Include(t => t.Topic)
            .AsQueryable();

        if (criteria.TopicId.HasValue)
        {
            query = query.Where(t => t.TopicId == criteria.TopicId);
        }

        query = ApplyTopicPeriodFilter(query, criteria);

        return await query
            .OrderBy(t => t.Year)
            .ThenBy(t => t.Month)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<JournalTrend>> GetJournalTrendsAsync(TrendFilterCriteria criteria)
    {
        var query = _context.JournalTrends
            .Include(t => t.Journal)
            .AsQueryable();

        if (criteria.JournalId.HasValue)
        {
            query = query.Where(t => t.JournalId == criteria.JournalId);
        }

        query = ApplyJournalPeriodFilter(query, criteria);

        return await query
            .OrderBy(t => t.Year)
            .ThenBy(t => t.Month)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<TrendDataPointDto>> GetPublicationTrendAsync(TrendFilterCriteria criteria)
    {
        var query = _context.ResearchPapers
            .Where(p => p.Status == PaperStatus.Available && p.PublicationDate.HasValue);

        if (criteria.YearFrom.HasValue)
        {
            query = query.Where(p => p.PublicationDate!.Value.Year >= criteria.YearFrom);
        }

        if (criteria.YearTo.HasValue)
        {
            query = query.Where(p => p.PublicationDate!.Value.Year <= criteria.YearTo);
        }

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

    private static List<TrendDataPointDto> BuildDataPointsWithGrowth(
        IEnumerable<(int Year, int Month, int PaperCount, int CitationCount)> items)
    {
        var result = new List<TrendDataPointDto>();
        var previousCount = 0;

        foreach (var item in items)
        {
            var growthRate = previousCount == 0
                ? 0
                : Math.Round(((item.PaperCount - previousCount) / (double)previousCount) * 100, 2);

            result.Add(new TrendDataPointDto
            {
                Year = item.Year,
                Month = item.Month,
                PaperCount = item.PaperCount,
                CitationCount = item.CitationCount,
                GrowthRate = growthRate,
                TrendingScore = Math.Round((item.PaperCount * 0.65) + (Math.Max(growthRate, 0) / 10.0) + (item.CitationCount / 120.0), 2)
            });

            previousCount = item.PaperCount;
        }

        return result;
    }
}
