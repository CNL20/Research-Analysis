using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.Services.Keywords;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Jobs;

/// <summary>
/// Recalculates KeywordTrends for a rolling 12-month window.
/// Runs daily via Hangfire — off the HTTP request path.
/// </summary>
public class RecalculateKeywordTrendsJob
{
    private const int RollingMonths = 12;

    private readonly ScholarTrendDbContext _context;
    private readonly ILogger<RecalculateKeywordTrendsJob> _logger;

    public RecalculateKeywordTrendsJob(
        ScholarTrendDbContext context,
        ILogger<RecalculateKeywordTrendsJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var windowStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMonths(-(RollingMonths - 1));

        var months = Enumerable.Range(0, RollingMonths)
            .Select(i => windowStart.AddMonths(i))
            .ToList();

        var keywordIds = await _context.PaperKeywords
            .Select(pk => pk.KeywordId)
            .Distinct()
            .ToListAsync(ct);

        if (keywordIds.Count == 0)
        {
            _logger.LogInformation("Keyword trend recalc skipped: no paper keywords");
            return;
        }

        var papers = await _context.ResearchPapers
            .AsNoTracking()
            .Where(p => p.PublicationDate.HasValue)
            .Select(p => new PaperTrendInput(
                p.Id,
                p.PublicationDate!.Value.Year,
                p.PublicationDate!.Value.Month,
                p.CitationCount ?? 0,
                p.PaperKeywords.Select(pk => pk.KeywordId).ToList()))
            .ToListAsync(ct);

        var existingTrends = await _context.KeywordTrends
            .Where(t => keywordIds.Contains(t.KeywordId))
            .Where(t =>
                t.Year > windowStart.Year ||
                (t.Year == windowStart.Year && t.Month >= windowStart.Month))
            .ToListAsync(ct);

        var existingByKey = existingTrends.ToDictionary(
            t => (t.KeywordId, t.Year, t.Month));

        var upserted = 0;

        foreach (var keywordId in keywordIds)
        {
            var previousCount = 0;

            foreach (var month in months)
            {
                var monthlyPapers = papers
                    .Where(p => p.Year == month.Year && p.Month == month.Month)
                    .Where(p => p.KeywordIds.Contains(keywordId))
                    .ToList();

                var paperCount = monthlyPapers.Count;
                var citationCount = monthlyPapers.Sum(p => p.CitationCount);
                var growthRate = KeywordTrendCalculator.CalculateGrowthRate(previousCount, paperCount);
                var trendingScore = KeywordTrendCalculator.CalculateTrendingScore(
                    paperCount, growthRate, citationCount);

                var key = (keywordId, month.Year, month.Month);
                if (existingByKey.TryGetValue(key, out var existing))
                {
                    existing.PaperCount = paperCount;
                    existing.CitationCount = citationCount;
                    existing.GrowthRate = growthRate;
                    existing.TrendingScore = trendingScore;
                }
                else
                {
                    await _context.KeywordTrends.AddAsync(new KeywordTrend
                    {
                        KeywordId = keywordId,
                        Year = month.Year,
                        Month = month.Month,
                        PaperCount = paperCount,
                        CitationCount = citationCount,
                        GrowthRate = growthRate,
                        TrendingScore = trendingScore
                    }, ct);
                }

                upserted++;
                previousCount = paperCount;
            }
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Keyword trend recalc completed: {KeywordCount} keywords, {MonthCount} months, {Upserted} rows",
            keywordIds.Count, months.Count, upserted);
    }

    private sealed record PaperTrendInput(
        int Id,
        int Year,
        int Month,
        int CitationCount,
        List<int> KeywordIds);
}
