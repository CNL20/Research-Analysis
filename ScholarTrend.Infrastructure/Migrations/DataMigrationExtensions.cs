using Microsoft.EntityFrameworkCore;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Migrations;

public static class DataMigrationExtensions
{
    /// <summary>
    /// Migrates data from PaperTopicExtraction to PaperAnalysis
    /// </summary>
    public static async Task MigratePaperTopicExtractionsToPaperAnalysis(this ScholarTrendDbContext context)
    {
        var extractions = await context.PaperTopicExtractions
            .Include(e => e.Paper)
            .ToListAsync();

        foreach (var extraction in extractions)
        {
            var existingAnalysis = await context.PaperAnalyses
                .FirstOrDefaultAsync(a => a.PaperId == extraction.PaperId);

            if (existingAnalysis != null)
                continue;

            var analysis = new PaperAnalysis
            {
                PaperId = extraction.PaperId,
                ResearchProblem = null,
                Method = null,
                Dataset = null,
                Metric = null,
                Contribution = null,
                MethodsJson = extraction.MethodsJson,
                DatasetsJson = extraction.DatasetsJson,
                LimitationsJson = extraction.LimitationsJson,
                FutureWorkJson = extraction.FutureWorkJson,
                DiscussionsJson = null,
                ConclusionsJson = null,
                EvidenceSentence = extraction.AchievementHint,
                Confidence = 70,
                AnalysisLevel = AnalysisLevels.Abstract,
                AnalysisSource = "MigratedFromExtraction",
                CreatedAt = extraction.ExtractedAt,
                UpdatedAt = DateTime.UtcNow
            };

            await context.PaperAnalyses.AddAsync(analysis);
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Assesses quality for all papers
    /// </summary>
    public static async Task AssessQualityForAllPapers(this ScholarTrendDbContext context)
    {
        var papers = await context.ResearchPapers
            .Where(p => !context.PaperQualities.Any(q => q.PaperId == p.Id))
            .ToListAsync();

        foreach (var paper in papers)
        {
            var hasPdf = !string.IsNullOrWhiteSpace(paper.PdfUrl);
            var hasAbstract = !string.IsNullOrWhiteSpace(paper.Abstract);
            var abstractLength = paper.Abstract?.Length ?? 0;
            var hasDoi = !string.IsNullOrWhiteSpace(paper.Doi);
            var hasKeywords = await context.PaperKeywords.AnyAsync(pk => pk.PaperId == paper.Id);
            var hasJournal = paper.JournalId.HasValue;
            var authorCount = await context.PaperAuthors.CountAsync(pa => pa.PaperId == paper.Id);
            var citationCount = paper.CitationCount ?? 0;

            int qualityScore = 0;
            if (hasPdf) qualityScore += 25;
            if (hasAbstract) qualityScore += 25;
            if (abstractLength > 200) qualityScore += 15;
            if (hasDoi) qualityScore += 10;
            if (hasKeywords) qualityScore += 10;
            if (hasJournal) qualityScore += 10;
            if (authorCount > 0) qualityScore += 5;

            var qualityGrade = qualityScore switch
            {
                >= 80 => QualityGrade.A,
                >= 60 => QualityGrade.B,
                >= 40 => QualityGrade.C,
                >= 20 => QualityGrade.D,
                _ => QualityGrade.F
            };

            var analysisLevel = hasPdf ? AnalysisLevels.Abstract : (hasAbstract ? AnalysisLevels.Abstract : AnalysisLevels.Metadata);

            var quality = new PaperQuality
            {
                PaperId = paper.Id,
                HasPdf = hasPdf,
                HasAbstract = hasAbstract,
                HasFullText = false,
                AbstractLength = abstractLength,
                AuthorCount = authorCount,
                HasDoi = hasDoi,
                HasKeywords = hasKeywords,
                HasJournal = hasJournal,
                CitationCount = citationCount,
                QualityScore = qualityScore,
                QualityGrade = qualityGrade,
                AnalysisLevel = analysisLevel,
                AssessedAt = DateTime.UtcNow
            };

            await context.PaperQualities.AddAsync(quality);
        }

        await context.SaveChangesAsync();
    }
}
