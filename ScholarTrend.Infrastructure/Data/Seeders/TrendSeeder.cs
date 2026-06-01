using Microsoft.EntityFrameworkCore;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Data.Seeders;

public static class TrendSeeder
{
    public static async Task SeedAsync(ScholarTrendDbContext context)
    {
        if (await context.KeywordTrends.AnyAsync() || await context.TopicTrends.AnyAsync() || await context.JournalTrends.AnyAsync())
        {
            return;
        }

        var papers = await context.ResearchPapers
            .Include(p => p.PaperKeywords)
            .Include(p => p.PaperTopics)
            .ToListAsync();

        var keywords = await context.Keywords.ToListAsync();
        var topics = await context.ResearchTopics.ToListAsync();
        var journals = await context.Journals.ToListAsync();

        var startMonth = new DateTime(2025, 6, 1);
        var months = Enumerable.Range(0, 12)
            .Select(offset => startMonth.AddMonths(offset))
            .ToList();

        var keywordTrends = BuildKeywordTrends(papers, keywords, months);
        var topicTrends = BuildTopicTrends(papers, topics, months);
        var journalTrends = BuildJournalTrends(papers, journals, months);

        await context.KeywordTrends.AddRangeAsync(keywordTrends);
        await context.TopicTrends.AddRangeAsync(topicTrends);
        await context.JournalTrends.AddRangeAsync(journalTrends);
        await context.SaveChangesAsync();
    }

    private static List<KeywordTrend> BuildKeywordTrends(
        List<ResearchPaper> papers,
        List<Keyword> keywords,
        List<DateTime> months)
    {
        var trends = new List<KeywordTrend>();

        foreach (var keyword in keywords)
        {
            var previousCount = 0;

            foreach (var month in months)
            {
                var monthlyPapers = papers
                    .Where(p => p.PublicationDate.HasValue
                                && p.PublicationDate.Value.Year == month.Year
                                && p.PublicationDate.Value.Month == month.Month)
                    .Where(p => p.PaperKeywords.Any(pk => pk.KeywordId == keyword.Id))
                    .ToList();

                var paperCount = monthlyPapers.Count;
                var citationCount = monthlyPapers.Sum(p => p.CitationCount ?? 0);
                var growthRate = previousCount == 0 ? 0 : ((paperCount - previousCount) / (double)previousCount) * 100.0;
                var trendingScore = Math.Round((paperCount * 0.65) + (Math.Max(growthRate, 0) / 10.0) + (citationCount / 120.0), 2);

                trends.Add(new KeywordTrend
                {
                    KeywordId = keyword.Id,
                    Year = month.Year,
                    Month = month.Month,
                    PaperCount = paperCount,
                    CitationCount = citationCount,
                    GrowthRate = Math.Round(growthRate, 2),
                    TrendingScore = trendingScore
                });

                previousCount = paperCount;
            }
        }

        return trends;
    }

    private static List<TopicTrend> BuildTopicTrends(
        List<ResearchPaper> papers,
        List<ResearchTopic> topics,
        List<DateTime> months)
    {
        var trends = new List<TopicTrend>();

        foreach (var topic in topics)
        {
            var previousCount = 0;

            foreach (var month in months)
            {
                var monthlyPapers = papers
                    .Where(p => p.PublicationDate.HasValue
                                && p.PublicationDate.Value.Year == month.Year
                                && p.PublicationDate.Value.Month == month.Month)
                    .Where(p => p.PaperTopics.Any(pt => pt.TopicId == topic.Id))
                    .ToList();

                var paperCount = monthlyPapers.Count;
                var citationCount = monthlyPapers.Sum(p => p.CitationCount ?? 0);
                var growthRate = previousCount == 0 ? 0 : ((paperCount - previousCount) / (double)previousCount) * 100.0;
                var trendingScore = Math.Round((paperCount * 0.65) + (Math.Max(growthRate, 0) / 10.0) + (citationCount / 120.0), 2);

                trends.Add(new TopicTrend
                {
                    TopicId = topic.Id,
                    Year = month.Year,
                    Month = month.Month,
                    PaperCount = paperCount,
                    CitationCount = citationCount,
                    GrowthRate = Math.Round(growthRate, 2),
                    TrendingScore = trendingScore
                });

                previousCount = paperCount;
            }
        }

        return trends;
    }

    private static List<JournalTrend> BuildJournalTrends(
        List<ResearchPaper> papers,
        List<Journal> journals,
        List<DateTime> months)
    {
        var trends = new List<JournalTrend>();

        foreach (var journal in journals)
        {
            var previousCount = 0;

            foreach (var month in months)
            {
                var monthlyPapers = papers
                    .Where(p => p.PublicationDate.HasValue
                                && p.PublicationDate.Value.Year == month.Year
                                && p.PublicationDate.Value.Month == month.Month)
                    .Where(p => p.JournalId == journal.Id)
                    .ToList();

                var paperCount = monthlyPapers.Count;
                var citationCount = monthlyPapers.Sum(p => p.CitationCount ?? 0);
                var growthRate = previousCount == 0 ? 0 : ((paperCount - previousCount) / (double)previousCount) * 100.0;
                var trendingScore = Math.Round((paperCount * 0.65) + (Math.Max(growthRate, 0) / 10.0) + (citationCount / 120.0), 2);

                trends.Add(new JournalTrend
                {
                    JournalId = journal.Id,
                    Year = month.Year,
                    Month = month.Month,
                    PaperCount = paperCount,
                    CitationCount = citationCount,
                    GrowthRate = Math.Round(growthRate, 2),
                    TrendingScore = trendingScore
                });

                previousCount = paperCount;
            }
        }

        return trends;
    }
}
