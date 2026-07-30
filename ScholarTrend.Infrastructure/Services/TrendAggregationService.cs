using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
/// Counts are aggregated in a single pass over papers; approve-triggered rebuilds
/// are debounced (60s) so consecutive approvals share one Hangfire job.
/// </summary>
public class TrendAggregationService : ITrendAggregationService
{
    private static readonly TimeSpan RebuildDebounce = TimeSpan.FromSeconds(60);
    private static readonly object PendingRebuildLock = new();
    private static string? _pendingRebuildJobId;

    private readonly ScholarTrendDbContext _context;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ITrendDashboardCacheInvalidator _cacheInvalidator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TrendAggregationService> _logger;

    public TrendAggregationService(
        ScholarTrendDbContext context,
        IBackgroundJobClient backgroundJobs,
        ITrendDashboardCacheInvalidator cacheInvalidator,
        IConfiguration configuration,
        ILogger<TrendAggregationService> logger)
    {
        _context = context;
        _backgroundJobs = backgroundJobs;
        _cacheInvalidator = cacheInvalidator;
        _configuration = configuration;
        _logger = logger;
    }

    private bool IsTrendRecalcEnabled =>
        _configuration.GetValue("Hangfire:TrendRecalcEnabled", true);

    /// <summary>
    /// Debounced enqueue: cancels any pending delayed rebuild and schedules a new one in 60s.
    /// Cron / ensure-built still enqueue immediately via their own entry points.
    /// </summary>
    public void ScheduleRebuild()
    {
        if (!IsTrendRecalcEnabled)
        {
            _logger.LogInformation("TrendRecalcEnabled=false — skip ScheduleRebuild after approve/sync");
            return;
        }

        lock (PendingRebuildLock)
        {
            if (!string.IsNullOrEmpty(_pendingRebuildJobId))
            {
                try
                {
                    _backgroundJobs.Delete(_pendingRebuildJobId);
                    _logger.LogDebug("Cancelled pending trend rebuild job {JobId}", _pendingRebuildJobId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not cancel pending trend rebuild job {JobId}", _pendingRebuildJobId);
                }

                _pendingRebuildJobId = null;
            }

            _pendingRebuildJobId = _backgroundJobs.Schedule<RecalculateTrendsJob>(
                job => job.RunAsync(CancellationToken.None),
                RebuildDebounce);

            _logger.LogInformation(
                "Scheduled debounced trend rebuild in {Seconds}s (job {JobId})",
                RebuildDebounce.TotalSeconds,
                _pendingRebuildJobId);
        }
    }

    public void ScheduleEnsureBuilt()
    {
        if (!IsTrendRecalcEnabled)
        {
            _logger.LogInformation("TrendRecalcEnabled=false — skip ScheduleEnsureBuilt");
            return;
        }

        _backgroundJobs.Enqueue<RecalculateTrendsJob>(job => job.EnsureBuiltAsync(CancellationToken.None));
        _logger.LogInformation("Scheduled trend ensure-built job");
    }

    public async Task RebuildAsync(CancellationToken ct = default)
    {
        lock (PendingRebuildLock)
        {
            _pendingRebuildJobId = null;
        }

        var papers = await LoadBrowsablePapersAsync(ct);
        var window = TrendPeriod.GetRebuildWindow(papers.Select(p => (p.Year, p.Month)));
        _logger.LogInformation(
            "Trend rebuild started for window {Start:yyyy-MM} .. {End:yyyy-MM} ({PaperCount} papers)",
            window.Start, window.End, papers.Count);

        var keywordCounts = BuildKeywordCounts(papers);
        var topicCounts = BuildTopicCounts(papers);
        var journalCounts = BuildJournalCounts(papers);

        var keywordIds = await UpsertKeywordTrendsAsync(keywordCounts, window, ct);
        await _context.SaveChangesAsync(ct);

        var topicIds = await UpsertTopicTrendsAsync(topicCounts, window, ct);
        await _context.SaveChangesAsync(ct);

        var journalIds = await UpsertJournalTrendsAsync(journalCounts, window, ct);
        await _context.SaveChangesAsync(ct);

        // Delete empty rows first (SQL bulk) so later prunes do not load ~millions of zeros into memory.
        await PruneZeroCountRowsAsync(ct);
        await PruneOutsideWindowAsync(window.Start, ct);
        await PruneOrphansInWindowAsync(window, keywordIds, topicIds, journalIds, ct);
        await _context.SaveChangesAsync(ct);
        _cacheInvalidator.Invalidate();
        _logger.LogInformation(
            "Trend rebuild completed: {PaperCount} browsable papers, {ActiveKeywordMonths} keyword-month cells",
            papers.Count, keywordCounts.Count);
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
        var endYear = window.End.Year;
        var endMonth = window.End.Month;

        var hasKeywordPapers = papers.Any(p => p.KeywordIds.Count > 0);
        var hasTopicPapers = papers.Any(p => p.TopicIds.Count > 0);
        var hasJournalPapers = papers.Any(p => p.JournalId.HasValue);

        // With sparse (non-zero-only) storage, start-month rows may be absent even when data is fresh.
        // Treat as fresh when end-of-window activity exists for each dimension that has papers.
        var keywordsFresh = !hasKeywordPapers || await _context.KeywordTrends.AnyAsync(
            t => t.Year == endYear && t.Month == endMonth && t.PaperCount > 0, ct);

        var topicsFresh = !hasTopicPapers || await _context.TopicTrends.AnyAsync(
            t => t.Year == endYear && t.Month == endMonth && t.PaperCount > 0, ct);

        var journalsFresh = !hasJournalPapers || await _context.JournalTrends.AnyAsync(
            t => t.Year == endYear && t.Month == endMonth && t.PaperCount > 0, ct);

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

    /// <summary>Single pass: each paper contributes once to every keyword it has.</summary>
    private static Dictionary<(int EntityId, int Year, int Month), MonthCounts> BuildKeywordCounts(
        List<PaperTrendInput> papers)
    {
        var map = new Dictionary<(int, int, int), MonthCounts>();
        foreach (var p in papers)
        {
            foreach (var keywordId in p.KeywordIds)
            {
                AddCount(map, keywordId, p.Year, p.Month, p.CitationCount);
            }
        }

        return map;
    }

    private static Dictionary<(int EntityId, int Year, int Month), MonthCounts> BuildTopicCounts(
        List<PaperTrendInput> papers)
    {
        var map = new Dictionary<(int, int, int), MonthCounts>();
        foreach (var p in papers)
        {
            foreach (var topicId in p.TopicIds)
            {
                AddCount(map, topicId, p.Year, p.Month, p.CitationCount);
            }
        }

        return map;
    }

    private static Dictionary<(int EntityId, int Year, int Month), MonthCounts> BuildJournalCounts(
        List<PaperTrendInput> papers)
    {
        var map = new Dictionary<(int, int, int), MonthCounts>();
        foreach (var p in papers)
        {
            if (!p.JournalId.HasValue)
            {
                continue;
            }

            AddCount(map, p.JournalId.Value, p.Year, p.Month, p.CitationCount);
        }

        return map;
    }

    private static void AddCount(
        Dictionary<(int EntityId, int Year, int Month), MonthCounts> map,
        int entityId,
        int year,
        int month,
        int citationCount)
    {
        var key = (entityId, year, month);
        if (map.TryGetValue(key, out var current))
        {
            map[key] = current with
            {
                PaperCount = current.PaperCount + 1,
                CitationCount = current.CitationCount + citationCount
            };
        }
        else
        {
            map[key] = new MonthCounts(1, citationCount);
        }
    }

    /// <summary>
    /// Upsert only months with PaperCount &gt; 0 (keys already present in <paramref name="counts"/>).
    /// Removes stale/zero rows for those entities so we do not store ~keyword×240 empty months.
    /// </summary>
    private async Task<List<int>> UpsertKeywordTrendsAsync(
        Dictionary<(int EntityId, int Year, int Month), MonthCounts> counts,
        TrendPeriod.TrendWindow window,
        CancellationToken ct)
    {
        var entityIds = counts.Keys.Select(k => k.EntityId).Distinct().ToList();
        if (entityIds.Count == 0) return entityIds;

        var existing = await _context.KeywordTrends
            .Where(t => entityIds.Contains(t.KeywordId) && t.PaperCount > 0)
            .Where(t => t.Year > window.Start.Year ||
                        (t.Year == window.Start.Year && t.Month >= window.Start.Month))
            .ToListAsync(ct);

        var byKey = existing
            .GroupBy(t => (t.KeywordId, t.Year, t.Month))
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var ((entityId, year, month), monthCounts) in counts)
        {
            var (prevYear, prevMonth) = PreviousMonth(year, month);
            counts.TryGetValue((entityId, prevYear, prevMonth), out var previous);
            var paperCount = monthCounts.PaperCount;
            var citationCount = monthCounts.CitationCount;
            var growthRate = KeywordTrendCalculator.CalculateGrowthRate(previous.PaperCount, paperCount);
            var score = KeywordTrendCalculator.CalculateTrendingScore(paperCount, growthRate, citationCount);
            var key = (entityId, year, month);

            if (byKey.TryGetValue(key, out var row))
            {
                row.PaperCount = paperCount;
                row.CitationCount = citationCount;
                row.GrowthRate = growthRate;
                row.TrendingScore = score;
                byKey.Remove(key);
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
        }

        // Leftover existing rows = zero/stale months no longer in counts
        if (byKey.Count > 0)
        {
            _context.KeywordTrends.RemoveRange(byKey.Values);
        }

        return entityIds;
    }

    private async Task<List<int>> UpsertTopicTrendsAsync(
        Dictionary<(int EntityId, int Year, int Month), MonthCounts> counts,
        TrendPeriod.TrendWindow window,
        CancellationToken ct)
    {
        var entityIds = counts.Keys.Select(k => k.EntityId).Distinct().ToList();
        if (entityIds.Count == 0) return entityIds;

        var existing = await _context.TopicTrends
            .Where(t => entityIds.Contains(t.TopicId) && t.PaperCount > 0)
            .Where(t => t.Year > window.Start.Year ||
                        (t.Year == window.Start.Year && t.Month >= window.Start.Month))
            .ToListAsync(ct);

        var byKey = existing
            .GroupBy(t => (t.TopicId, t.Year, t.Month))
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var ((entityId, year, month), monthCounts) in counts)
        {
            var (prevYear, prevMonth) = PreviousMonth(year, month);
            counts.TryGetValue((entityId, prevYear, prevMonth), out var previous);
            var paperCount = monthCounts.PaperCount;
            var citationCount = monthCounts.CitationCount;
            var growthRate = KeywordTrendCalculator.CalculateGrowthRate(previous.PaperCount, paperCount);
            var score = KeywordTrendCalculator.CalculateTrendingScore(paperCount, growthRate, citationCount);
            var key = (entityId, year, month);

            if (byKey.TryGetValue(key, out var row))
            {
                row.PaperCount = paperCount;
                row.CitationCount = citationCount;
                row.GrowthRate = growthRate;
                row.TrendingScore = score;
                byKey.Remove(key);
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
        }

        if (byKey.Count > 0)
        {
            _context.TopicTrends.RemoveRange(byKey.Values);
        }

        return entityIds;
    }

    private async Task<List<int>> UpsertJournalTrendsAsync(
        Dictionary<(int EntityId, int Year, int Month), MonthCounts> counts,
        TrendPeriod.TrendWindow window,
        CancellationToken ct)
    {
        var entityIds = counts.Keys.Select(k => k.EntityId).Distinct().ToList();
        if (entityIds.Count == 0) return entityIds;

        var existing = await _context.JournalTrends
            .Where(t => entityIds.Contains(t.JournalId) && t.PaperCount > 0)
            .Where(t => t.Year > window.Start.Year ||
                        (t.Year == window.Start.Year && t.Month >= window.Start.Month))
            .ToListAsync(ct);

        var byKey = existing
            .GroupBy(t => (t.JournalId, t.Year, t.Month))
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var ((entityId, year, month), monthCounts) in counts)
        {
            var (prevYear, prevMonth) = PreviousMonth(year, month);
            counts.TryGetValue((entityId, prevYear, prevMonth), out var previous);
            var paperCount = monthCounts.PaperCount;
            var citationCount = monthCounts.CitationCount;
            var growthRate = KeywordTrendCalculator.CalculateGrowthRate(previous.PaperCount, paperCount);
            var score = KeywordTrendCalculator.CalculateTrendingScore(paperCount, growthRate, citationCount);
            var key = (entityId, year, month);

            if (byKey.TryGetValue(key, out var row))
            {
                row.PaperCount = paperCount;
                row.CitationCount = citationCount;
                row.GrowthRate = growthRate;
                row.TrendingScore = score;
                byKey.Remove(key);
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
        }

        if (byKey.Count > 0)
        {
            _context.JournalTrends.RemoveRange(byKey.Values);
        }

        return entityIds;
    }

    private static (int Year, int Month) PreviousMonth(int year, int month)
    {
        return month == 1 ? (year - 1, 12) : (year, month - 1);
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
            .ExecuteDeleteAsync(ct);

        var orphanTopics = await _context.TopicTrends
            .Where(t =>
                (t.Year > startYear || (t.Year == startYear && t.Month >= startMonth))
                && (t.Year < endYear || (t.Year == endYear && t.Month <= endMonth)))
            .Where(t => !topicIds.Contains(t.TopicId))
            .ExecuteDeleteAsync(ct);

        var orphanJournals = await _context.JournalTrends
            .Where(t =>
                (t.Year > startYear || (t.Year == startYear && t.Month >= startMonth))
                && (t.Year < endYear || (t.Year == endYear && t.Month <= endMonth)))
            .Where(t => !journalIds.Contains(t.JournalId))
            .ExecuteDeleteAsync(ct);

        if (orphanKeywords + orphanTopics + orphanJournals > 0)
        {
            _logger.LogInformation(
                "Pruned in-window orphan trend rows: keywords={Kw}, topics={Topics}, journals={Journals}",
                orphanKeywords, orphanTopics, orphanJournals);
        }
    }

    private async Task PruneZeroCountRowsAsync(CancellationToken ct)
    {
        // Bulk delete empty calendar months left by older rebuilds (can be millions of rows).
        var kw = await _context.KeywordTrends.Where(t => t.PaperCount == 0).ExecuteDeleteAsync(ct);
        var topics = await _context.TopicTrends.Where(t => t.PaperCount == 0).ExecuteDeleteAsync(ct);
        var journals = await _context.JournalTrends.Where(t => t.PaperCount == 0).ExecuteDeleteAsync(ct);
        if (kw + topics + journals > 0)
        {
            _logger.LogInformation(
                "Pruned zero-count trend rows: keywords={Kw}, topics={Topics}, journals={Journals}",
                kw, topics, journals);
        }
    }

    private async Task PruneOutsideWindowAsync(DateTime windowStart, CancellationToken ct)
    {
        var staleKeywords = await _context.KeywordTrends
            .Where(t => t.Year < windowStart.Year ||
                        (t.Year == windowStart.Year && t.Month < windowStart.Month))
            .ExecuteDeleteAsync(ct);

        var staleTopics = await _context.TopicTrends
            .Where(t => t.Year < windowStart.Year ||
                        (t.Year == windowStart.Year && t.Month < windowStart.Month))
            .ExecuteDeleteAsync(ct);

        var staleJournals = await _context.JournalTrends
            .Where(t => t.Year < windowStart.Year ||
                        (t.Year == windowStart.Year && t.Month < windowStart.Month))
            .ExecuteDeleteAsync(ct);

        if (staleKeywords + staleTopics + staleJournals > 0)
        {
            _logger.LogInformation(
                "Pruned trend rows outside window: keywords={Kw}, topics={Topics}, journals={Journals}",
                staleKeywords, staleTopics, staleJournals);
        }
    }

    private readonly record struct MonthCounts(int PaperCount, int CitationCount);

    private sealed record PaperTrendInput(
        int Id,
        int Year,
        int Month,
        int CitationCount,
        int? JournalId,
        List<int> KeywordIds,
        List<int> TopicIds);
}
