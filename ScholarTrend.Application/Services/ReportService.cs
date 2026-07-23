using System.Text;
using ScholarTrend.Application.DTOs.Reports;
using ScholarTrend.Application.DTOs.Trends;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Application.Services.Reports;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Services;

public class ReportService : IReportService
{
    private readonly IStatisticsRepository _statistics;
    private readonly ITrendRepository _trends;

    public ReportService(IStatisticsRepository statistics, ITrendRepository trends)
    {
        _statistics = statistics;
        _trends = trends;
    }

    public async Task<PublicationReportDto> GenerateReportAsync(ReportFilterRequest filter)
    {
        var groupBy = filter.GroupBy.ToLowerInvariant();
        var yearFrom = filter.YearFrom ?? 2020;
        var yearTo = filter.YearTo ?? DateTime.UtcNow.Year;
        var top = filter.Top;

        var items = groupBy switch
        {
            "keyword" => (await _statistics.GetReportByKeywordAsync(yearFrom, yearTo)).ToList(),
            "topic" => (await _statistics.GetReportByTopicAsync(yearFrom, yearTo)).ToList(),
            "journal" => (await _statistics.GetReportByJournalAsync(yearFrom, yearTo)).ToList(),
            _ => (await _statistics.GetReportByYearAsync(yearFrom, yearTo)).ToList()
        };

        if (groupBy is "keyword" or "topic" or "journal")
        {
            // Candidate pool before score re-rank (keeps work bounded when Top is set).
            if (top is > 0)
            {
                var candidateCount = Math.Max(top.Value * 10, 50);
                items = items.Take(candidateCount).ToList();
            }

            await EnrichWithTrendsAsync(items, groupBy, yearFrom, yearTo);
            await EnrichWithReliabilityAsync(items, groupBy, yearFrom, yearTo);

            items = items
                .OrderByDescending(i => i.TrendingScore ?? 0)
                .ThenByDescending(i => i.PaperCount)
                .ThenByDescending(i => i.GrowthRate ?? 0)
                .ToList();

            for (var i = 0; i < items.Count; i++)
            {
                items[i].Rank = i + 1;
                items[i].Suggestion = ResearchSuggestionRules.Evaluate(
                    items[i].PaperCount,
                    items[i].GrowthRate,
                    items[i].TrendingScore);
            }

            if (top is > 0)
                items = items.Take(top.Value).ToList();
        }
        else
        {
            // Year breakdown: scale only + optional Top by paper count.
            foreach (var item in items)
            {
                item.Suggestion = ResearchSuggestionRules.Neutral;
            }

            if (top is > 0)
                items = items.OrderByDescending(i => i.PaperCount).Take(top.Value).ToList();

            for (var i = 0; i < items.Count; i++)
                items[i].Rank = i + 1;
        }

        var totalCitations = items.Sum(i => i.TotalCitations);
        var reliabilityValues = items
            .Where(i => i.ReliabilityPercent.HasValue)
            .Select(i => i.ReliabilityPercent!.Value)
            .ToList();

        return new PublicationReportDto
        {
            GroupBy = groupBy,
            YearFrom = yearFrom,
            YearTo = yearTo,
            Top = top,
            TotalPapers = items.Sum(i => i.PaperCount),
            TotalCitations = totalCitations,
            AverageReliability = reliabilityValues.Count > 0
                ? Math.Round(reliabilityValues.Average(), 1)
                : null,
            Items = items,
            GeneratedAt = DateTime.UtcNow
        };
    }

    public byte[] ExportCsv(PublicationReportDto report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ScholarTrend Publication Report");
        sb.AppendLine($"groupBy,{EscapeCsv(report.GroupBy)}");
        sb.AppendLine($"yearFrom,{report.YearFrom?.ToString() ?? ""}");
        sb.AppendLine($"yearTo,{report.YearTo?.ToString() ?? ""}");
        sb.AppendLine($"top,{report.Top?.ToString() ?? ""}");
        sb.AppendLine($"totalPapers,{report.TotalPapers}");
        sb.AppendLine($"totalCitations,{report.TotalCitations}");
        sb.AppendLine($"averageReliability,{report.AverageReliability?.ToString("0.#") ?? ""}");
        sb.AppendLine($"generatedAt,{report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine(
            "rank,id,key,paperCount,totalCitations,growthRate,trendingScore,periodYear,periodMonth,reliabilityPercent,suggestion");

        foreach (var item in report.Items)
        {
            sb.AppendLine(string.Join(',',
                item.Rank?.ToString() ?? "",
                item.Id?.ToString() ?? "",
                EscapeCsv(item.Key),
                item.PaperCount,
                item.TotalCitations,
                item.GrowthRate?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                item.TrendingScore?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                item.PeriodYear?.ToString() ?? "",
                item.PeriodMonth?.ToString() ?? "",
                item.ReliabilityPercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                EscapeCsv(item.Suggestion ?? "")));
        }

        // UTF-8 BOM helps Excel open Vietnamese/Unicode names correctly.
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
        return result;
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private async Task EnrichWithTrendsAsync(
        List<ReportGroupItemDto> items,
        string groupBy,
        int yearFrom,
        int yearTo)
    {
        var criteria = new TrendFilterCriteria
        {
            YearFrom = yearFrom,
            YearTo = yearTo,
            MonthFrom = 1,
            MonthTo = 12
        };

        var metricsById = groupBy switch
        {
            "keyword" => PickBestTrendMetrics(
                (await _trends.GetKeywordTrendsAsync(criteria))
                    .Select(t => (t.KeywordId, t.Year, t.Month, t.PaperCount, t.GrowthRate, t.TrendingScore))),
            "topic" => PickBestTrendMetrics(
                (await _trends.GetTopicTrendsAsync(criteria))
                    .Select(t => (t.TopicId, t.Year, t.Month, t.PaperCount, t.GrowthRate, t.TrendingScore))),
            "journal" => PickBestTrendMetrics(
                (await _trends.GetJournalTrendsAsync(criteria))
                    .Select(t => (t.JournalId, t.Year, t.Month, t.PaperCount, t.GrowthRate, t.TrendingScore))),
            _ => new Dictionary<int, (int Year, int Month, double GrowthRate, double TrendingScore)>()
        };

        foreach (var item in items)
        {
            if (!item.Id.HasValue) continue;
            if (!metricsById.TryGetValue(item.Id.Value, out var m)) continue;

            item.GrowthRate = m.GrowthRate;
            item.TrendingScore = m.TrendingScore;
            item.PeriodYear = m.Year;
            item.PeriodMonth = m.Month;
        }
    }

    private async Task EnrichWithReliabilityAsync(
        List<ReportGroupItemDto> items,
        string groupBy,
        int yearFrom,
        int yearTo)
    {
        var ids = items.Where(i => i.Id.HasValue).Select(i => i.Id!.Value).ToList();
        if (ids.Count == 0) return;

        var map = groupBy switch
        {
            "keyword" => await _statistics.GetKeywordReliabilityAsync(ids, yearFrom, yearTo),
            "topic" => await _statistics.GetTopicReliabilityAsync(ids, yearFrom, yearTo),
            "journal" => await _statistics.GetJournalReliabilityAsync(ids, yearFrom, yearTo),
            _ => (IReadOnlyDictionary<int, double>)new Dictionary<int, double>()
        };

        foreach (var item in items)
        {
            if (item.Id.HasValue && map.TryGetValue(item.Id.Value, out var reliability))
                item.ReliabilityPercent = reliability;
        }
    }

    /// <summary>
    /// Same idea as TrendService.BuildTopItems: per entity prefer a month with PaperCount &gt; 0 and highest score.
    /// </summary>
    private static Dictionary<int, (int Year, int Month, double GrowthRate, double TrendingScore)> PickBestTrendMetrics(
        IEnumerable<(int Id, int Year, int Month, int PaperCount, double GrowthRate, double TrendingScore)> rows)
    {
        return rows
            .GroupBy(r => r.Id)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var active = g.Where(t => t.PaperCount > 0).ToList();
                    var pool = active.Count > 0 ? active : g.ToList();
                    var best = pool
                        .OrderByDescending(t => t.TrendingScore)
                        .ThenByDescending(t => t.PaperCount)
                        .ThenByDescending(t => t.Year)
                        .ThenByDescending(t => t.Month)
                        .First();
                    return (best.Year, best.Month, best.GrowthRate, best.TrendingScore);
                });
    }
}
