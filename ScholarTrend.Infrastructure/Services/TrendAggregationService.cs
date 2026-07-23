using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Services.Keywords;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Domain.Enums;
using ScholarTrend.Infrastructure.Data;
using ScholarTrend.Infrastructure.Jobs;

namespace ScholarTrend.Infrastructure.Services;

/// <summary>
/// Rebuilds keyword, topic, and journal trend tables with one shared formula,
/// browsable paper status filter, and full paper history window (capped).
/// </summary>
public class TrendAggregationService : ITrendAggregationService
{
    private readonly ScholarTrendDbContext _context;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ITrendDashboardCacheInvalidator _cacheInvalidator;
    private readonly ILogger<TrendAggregationService> _logger;

    public TrendAggregationService(
        ScholarTrendDbContext context,
        IBackgroundJobClient backgroundJobs,
        ITrendDashboardCacheInvalidator cacheInvalidator,
        ILogger<TrendAggregationService> logger)
    {
        _context = context;
        _backgroundJobs = backgroundJobs;
        _cacheInvalidator = cacheInvalidator;
        _logger = logger;
    }

    public void ScheduleRebuild()
    {
        _backgroundJobs.Enqueue<RecalculateTrendsJob>(job => job.RunAsync(CancellationToken.None));
        _logger.LogInformation("Scheduled trend rebuild job");
    }

    public void ScheduleEnsureBuilt()
    {
        _backgroundJobs.Enqueue<RecalculateTrendsJob>(job => job.EnsureBuiltAsync(CancellationToken.None));
        _logger.LogInformation("Scheduled trend ensure-built job");
    }

    public async Task RebuildAsync(CancellationToken ct = default)
    {
        var papers = await LoadBrowsablePapersAsync(ct);
        var window = TrendPeriod.GetRebuildWindow(papers.Select(p => (p.Year, p.Month)));
        _logger.LogInformation(
            "Trend rebuild started for window {Start:yyyy-MM} .. {End:yyyy-MM} ({PaperCount} papers)",
            window.Start, window.End, papers.Count);

        var keywordIds = await RebuildKeywordTrendsAsync(papers, window, ct);
        await _context.SaveChangesAsync(ct); // Save keywords first to reduce batch size

        var topicIds = await RebuildTopicTrendsAsync(papers, window, ct);
        await _context.SaveChangesAsync(ct); // Save topics

        var journalIds = await RebuildJournalTrendsAsync(papers, window, ct);
        await _context.SaveChangesAsync(ct); // Save journals

        await PruneOutsideWindowAsync(window.Start, ct);
        await PruneOrphansInWindowAsync(window, keywordIds, topicIds, journalIds, ct);
        await _context.SaveChangesAsync(ct); // Save prunes
        _cacheInvalidator.Invalidate();
        _logger.LogInformation(
            "Trend rebuild completed: {PaperCount} browsable papers, {MonthCount} months stored",
            papers.Count, window.Months.Count);
    }

    public async Task EnsureBuiltAsync(CancellationToken ct = default)
    {
        if (await IsTrendDataFreshAsync(ct))
        {
            _logger.LogInformation("Trend ensure-built skipped: tables aligned with paper history window");
            return;
        }

        _logger.LogInformation("Trend ensure-built running: tables empty or stale for paper history");
        await RebuildAsync(ct);
    }

    private async Task<bool> IsTrendDataFreshAsync(CancellationToken ct)
    {
        var papers = await LoadBrowsablePapersAsync(ct);
        if (papers.Count == 0)
        {
            return await _context.KeywordTrends.AnyAsync(ct)
                   && await _context.TopicTrends.AnyAsync(ct)
                   && await _context.JournalTrends.AnyAsync(ct);
        }

        var window = TrendPeriod.GetRebuildWindow(papers.Select(p => (p.Year, p.Month)));
        var startYear = window.Start.Year;
        var startMonth = window.Start.Month;
        var endYear = window.End.Year;
        var endMonth = window.End.Month;

        var hasKeywordPapers = papers.Any(p => p.KeywordIds.Count > 0);
        var hasTopicPapers = papers.Any(p => p.TopicIds.Count > 0);
        var hasJournalPapers = papers.Any(p => p.JournalId.HasValue);

        var keywordsFresh = !hasKeywordPapers || (
            await _context.KeywordTrends.AnyAsync(
                t => t.Year == startYear && t.Month == startMonth, ct)
            && await _context.KeywordTrends.AnyAsync(
                t => t.Year == endYear && t.Month == endMonth, ct));

        var topicsFresh = !hasTopicPapers || (
            await _context.TopicTrends.AnyAsync(
                t => t.Year == startYear && t.Month == startMonth, ct)
            && await _context.TopicTrends.AnyAsync(
                t => t.Year == endYear && t.Month == endMonth, ct));

        var journalsFresh = !hasJournalPapers || (
            await _context.JournalTrends.AnyAsync(
                t => t.Year == startYear && t.Month == startMonth, ct)
            && await _context.JournalTrends.AnyAsync(
                t => t.Year == endYear && t.Month == endMonth, ct));

        return keywordsFresh && topicsFresh && journalsFresh;
    }

    private async Task<List<PaperTrendInput>> LoadBrowsablePapersAsync(CancellationToken ct)
    {
        var browsable = PaperStatusRules.Browsable;

        return await _context.ResearchPapers
            .AsNoTracking()
            .Where(p => p.PublicationDate.HasValue && browsable.Contains(p.Status))
            .Select(p => new PaperTrendInput(
                p.Id,
                p.PublicationDate!.Value.Year,
                p.PublicationDate!.Value.Month,
                p.CitationCount ?? 0,
                p.JournalId,
                p.PaperKeywords.Select(pk => pk.KeywordId).ToList(),
                p.PaperTopics.Select(pt => pt.TopicId).ToList()))
            .ToListAsync(ct);
    }

    private async Task<List<int>> RebuildKeywordTrendsAsync(
        List<PaperTrendInput> papers,
        TrendPeriod.TrendWindow window,
        CancellationToken ct)
    {
        var entityIds = papers.SelectMany(p => p.KeywordIds).Distinct().ToList();
        if (entityIds.Count == 0) return entityIds;

        var existing = await _context.KeywordTrends
            .Where(t => entityIds.Contains(t.KeywordId))
            .Where(t => t.Year > window.Start.Year ||
                        (t.Year == window.Start.Year && t.Month >= window.Start.Month))
            .ToListAsync(ct);

        var byKey = existing.ToDictionary(t => (t.KeywordId, t.Year, t.Month));

        foreach (var entityId in entityIds)
        {
            var previousCount = 0;
            foreach (var (year, month) in window.Months)
            {
                var monthly = papers.Where(p =>
                    p.Year == year && p.Month == month && p.KeywordIds.Contains(entityId)).ToList();
                var (paperCount, citationCount, growthRate, score) = ComputeMetrics(monthly, previousCount);
                var key = (entityId, year, month);

                if (byKey.TryGetValue(key, out var row))
                {
                    row.PaperCount = paperCount;
                    row.CitationCount = citationCount;
                    row.GrowthRate = growthRate;
                    row.TrendingScore = score;
                }
                else
                {
                    await _context.KeywordTrends.AddAsync(new KeywordTrend
                    {
                        KeywordId = entityId,
                        Year = year,
                        Month = month,
                        PaperCount = paperCount,
                        CitationCount = citationCount,
                        GrowthRate = growthRate,
                        TrendingScore = score
                    }, ct);
                }

                previousCount = paperCount;
            }
        }

        return entityIds;
    }

    private async Task<List<int>> RebuildTopicTrendsAsync(
        List<PaperTrendInput> papers,
        TrendPeriod.TrendWindow window,
        CancellationToken ct)
    {
        var entityIds = papers.SelectMany(p => p.TopicIds).Distinct().ToList();
        if (entityIds.Count == 0) return entityIds;

        var existing = await _context.TopicTrends
            .Where(t => entityIds.Contains(t.TopicId))
            .Where(t => t.Year > window.Start.Year ||
                        (t.Year == window.Start.Year && t.Month >= window.Start.Month))
            .ToListAsync(ct);

        var byKey = existing.ToDictionary(t => (t.TopicId, t.Year, t.Month));

        foreach (var entityId in entityIds)
        {
            var previousCount = 0;
            foreach (var (year, month) in window.Months)
            {
                var monthly = papers.Where(p =>
                    p.Year == year && p.Month == month && p.TopicIds.Contains(entityId)).ToList();
                var (paperCount, citationCount, growthRate, score) = ComputeMetrics(monthly, previousCount);
                var key = (entityId, year, month);

                if (byKey.TryGetValue(key, out var row))
                {
                    row.PaperCount = paperCount;
                    row.CitationCount = citationCount;
                    row.GrowthRate = growthRate;
                    row.TrendingScore = score;
                }
                else
                {
                    await _context.TopicTrends.AddAsync(new TopicTrend
                    {
                        TopicId = entityId,
                        Year = year,
                        Month = month,
                        PaperCount = paperCount,
                        CitationCount = citationCount,
                        GrowthRate = growthRate,
                        TrendingScore = score
                    }, ct);
                }

                previousCount = paperCount;
            }
        }

        return entityIds;
    }

    private async Task<List<int>> RebuildJournalTrendsAsync(
        List<PaperTrendInput> papers,
        TrendPeriod.TrendWindow window,
        CancellationToken ct)
    {
        var entityIds = papers
            .Where(p => p.JournalId.HasValue)
            .Select(p => p.JournalId!.Value)
            .Distinct()
            .ToList();
        if (entityIds.Count == 0) return entityIds;

        var existing = await _context.JournalTrends
            .Where(t => entityIds.Contains(t.JournalId))
            .Where(t => t.Year > window.Start.Year ||
                        (t.Year == window.Start.Year && t.Month >= window.Start.Month))
            .ToListAsync(ct);

        var byKey = existing.ToDictionary(t => (t.JournalId, t.Year, t.Month));

        foreach (var entityId in entityIds)
        {
            var previousCount = 0;
            foreach (var (year, month) in window.Months)
            {
                var monthly = papers.Where(p =>
                    p.Year == year && p.Month == month && p.JournalId == entityId).ToList();
                var (paperCount, citationCount, growthRate, score) = ComputeMetrics(monthly, previousCount);
                var key = (entityId, year, month);

                if (byKey.TryGetValue(key, out var row))
                {
                    row.PaperCount = paperCount;
                    row.CitationCount = citationCount;
                    row.GrowthRate = growthRate;
                    row.TrendingScore = score;
                }
                else
                {
                    await _context.JournalTrends.AddAsync(new JournalTrend
                    {
                        JournalId = entityId,
                        Year = year,
                        Month = month,
                        PaperCount = paperCount,
                        CitationCount = citationCount,
                        GrowthRate = growthRate,
                        TrendingScore = score
                    }, ct);
                }

                previousCount = paperCount;
            }
        }

        return entityIds;
    }

    private async Task PruneOrphansInWindowAsync(
        TrendPeriod.TrendWindow window,
        IReadOnlyCollection<int> keywordIds,
        IReadOnlyCollection<int> topicIds,
        IReadOnlyCollection<int> journalIds,
        CancellationToken ct)
    {
        var startYear = window.Start.Year;
        var startMonth = window.Start.Month;
        var endYear = window.End.Year;
        var endMonth = window.End.Month;

        var orphanKeywords = await _context.KeywordTrends
            .Where(t =>
                (t.Year > startYear || (t.Year == startYear && t.Month >= startMonth))
                && (t.Year < endYear || (t.Year == endYear && t.Month <= endMonth)))
            .Where(t => !keywordIds.Contains(t.KeywordId))
            .ToListAsync(ct);
        _context.KeywordTrends.RemoveRange(orphanKeywords);

        var orphanTopics = await _context.TopicTrends
            .Where(t =>
                (t.Year > startYear || (t.Year == startYear && t.Month >= startMonth))
                && (t.Year < endYear || (t.Year == endYear && t.Month <= endMonth)))
            .Where(t => !topicIds.Contains(t.TopicId))
            .ToListAsync(ct);
        _context.TopicTrends.RemoveRange(orphanTopics);

        var orphanJournals = await _context.JournalTrends
            .Where(t =>
                (t.Year > startYear || (t.Year == startYear && t.Month >= startMonth))
                && (t.Year < endYear || (t.Year == endYear && t.Month <= endMonth)))
            .Where(t => !journalIds.Contains(t.JournalId))
            .ToListAsync(ct);
        _context.JournalTrends.RemoveRange(orphanJournals);

        var total = orphanKeywords.Count + orphanTopics.Count + orphanJournals.Count;
        if (total > 0)
        {
            _logger.LogInformation(
                "Pruned in-window orphan trend rows: keywords={Kw}, topics={Topics}, journals={Journals}",
                orphanKeywords.Count, orphanTopics.Count, orphanJournals.Count);
        }
    }

    private async Task PruneOutsideWindowAsync(DateTime windowStart, CancellationToken ct)
    {
        var staleKeywords = await _context.KeywordTrends
            .Where(t => t.Year < windowStart.Year ||
                        (t.Year == windowStart.Year && t.Month < windowStart.Month))
            .ToListAsync(ct);
        _context.KeywordTrends.RemoveRange(staleKeywords);

        var staleTopics = await _context.TopicTrends
            .Where(t => t.Year < windowStart.Year ||
                        (t.Year == windowStart.Year && t.Month < windowStart.Month))
            .ToListAsync(ct);
        _context.TopicTrends.RemoveRange(staleTopics);

        var staleJournals = await _context.JournalTrends
            .Where(t => t.Year < windowStart.Year ||
                        (t.Year == windowStart.Year && t.Month < windowStart.Month))
            .ToListAsync(ct);
        _context.JournalTrends.RemoveRange(staleJournals);

        if (staleKeywords.Count + staleTopics.Count + staleJournals.Count > 0)
        {
            _logger.LogInformation(
                "Pruned trend rows outside window: keywords={Kw}, topics={Topics}, journals={Journals}",
                staleKeywords.Count, staleTopics.Count, staleJournals.Count);
        }
    }

    private static (int PaperCount, int CitationCount, double GrowthRate, double Score) ComputeMetrics(
        List<PaperTrendInput> monthly,
        int previousCount)
    {
        var paperCount = monthly.Count;
        var citationCount = monthly.Sum(p => p.CitationCount);
        var growthRate = KeywordTrendCalculator.CalculateGrowthRate(previousCount, paperCount);
        var score = KeywordTrendCalculator.CalculateTrendingScore(paperCount, growthRate, citationCount);
        return (paperCount, citationCount, growthRate, score);
    }

    private sealed record PaperTrendInput(
        int Id,
        int Year,
        int Month,
        int CitationCount,
        int? JournalId,
        List<int> KeywordIds,
        List<int> TopicIds);
}
