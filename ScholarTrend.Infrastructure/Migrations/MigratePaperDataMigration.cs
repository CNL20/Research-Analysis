using Microsoft.EntityFrameworkCore;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Migrations;

public class MigratePaperDataMigration
{
    private readonly ScholarTrendDbContext _context;

    public MigratePaperDataMigration(ScholarTrendDbContext context)
    {
        _context = context;
    }

    public async Task RunMigrationAsync()
    {
        Console.WriteLine("Starting data migration...");

        // Migrate PaperTopicExtraction to PaperAnalysis
        await MigrateExtractionsAsync();

        // Assess quality for all papers
        await AssessQualityAsync();

        Console.WriteLine("Data migration completed.");
    }

    private async Task MigrateExtractionsAsync()
    {
        var count = await _context.PaperTopicExtractions.CountAsync();
        Console.WriteLine($"Found {count} PaperTopicExtraction records to migrate...");

        var migrated = 0;
        var skipped = 0;

        var extractions = await _context.PaperTopicExtractions.ToListAsync();
        foreach (var extraction in extractions)
        {
            var existingAnalysis = await _context.PaperAnalyses
                .FirstOrDefaultAsync(a => a.PaperId == extraction.PaperId);

            if (existingAnalysis != null)
            {
                skipped++;
                continue;
            }

            var analysis = new Domain.Entities.PaperAnalysis
            {
                PaperId = extraction.PaperId,
                MethodsJson = extraction.MethodsJson,
                DatasetsJson = extraction.DatasetsJson,
                LimitationsJson = extraction.LimitationsJson,
                FutureWorkJson = extraction.FutureWorkJson,
                EvidenceSentence = extraction.AchievementHint,
                Confidence = 70,
                AnalysisLevel = Domain.Entities.AnalysisLevels.Abstract,
                AnalysisSource = "MigratedFromExtraction",
                CreatedAt = extraction.ExtractedAt,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.PaperAnalyses.AddAsync(analysis);
            migrated++;

            if (migrated % 100 == 0)
            {
                await _context.SaveChangesAsync();
                Console.WriteLine($"Migrated {migrated}/{count - skipped} records...");
            }
        }

        await _context.SaveChangesAsync();
        Console.WriteLine($"Migrated {migrated} records, skipped {skipped} existing.");
    }

    private async Task AssessQualityAsync()
    {
        var papers = await _context.ResearchPapers.ToListAsync();
        Console.WriteLine($"Assessing quality for {papers.Count} papers...");

        var assessed = 0;
        var skipped = 0;

        foreach (var paper in papers)
        {
            var existingQuality = await _context.PaperQualities
                .FirstOrDefaultAsync(q => q.PaperId == paper.Id);

            if (existingQuality != null)
            {
                skipped++;
                continue;
            }

            var hasPdf = !string.IsNullOrWhiteSpace(paper.PdfUrl);
            var hasAbstract = !string.IsNullOrWhiteSpace(paper.Abstract);
            var abstractLength = paper.Abstract?.Length ?? 0;
            var hasDoi = !string.IsNullOrWhiteSpace(paper.Doi);
            var hasKeywords = await _context.PaperKeywords.AnyAsync(pk => pk.PaperId == paper.Id);
            var hasJournal = paper.JournalId.HasValue;
            var authorCount = await _context.PaperAuthors.CountAsync(pa => pa.PaperId == paper.Id);
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
                >= 80 => Domain.Entities.QualityGrade.A,
                >= 60 => Domain.Entities.QualityGrade.B,
                >= 40 => Domain.Entities.QualityGrade.C,
                >= 20 => Domain.Entities.QualityGrade.D,
                _ => Domain.Entities.QualityGrade.F
            };

            var analysisLevel = hasPdf ? Domain.Entities.AnalysisLevels.Abstract : (hasAbstract ? Domain.Entities.AnalysisLevels.Abstract : Domain.Entities.AnalysisLevels.Metadata);

            var quality = new Domain.Entities.PaperQuality
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

            await _context.PaperQualities.AddAsync(quality);
            assessed++;

            if (assessed % 100 == 0)
            {
                await _context.SaveChangesAsync();
                Console.WriteLine($"Assessed quality for {assessed}/{papers.Count - skipped} papers...");
            }
        }

        await _context.SaveChangesAsync();
        Console.WriteLine($"Assessed quality for {assessed} papers, skipped {skipped} existing.");
    }
}
