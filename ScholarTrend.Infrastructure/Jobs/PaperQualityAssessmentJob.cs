using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.GapAnalysis;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Jobs;

public class PaperQualityAssessmentJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaperQualityAssessmentJob> _logger;
    private const int BatchSize = 100;

    public PaperQualityAssessmentJob(
        IServiceScopeFactory scopeFactory,
        ILogger<PaperQualityAssessmentJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task AssessAllPapersAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting paper quality assessment for all topics...");

        using var scope = _scopeFactory.CreateScope();
        var topicRepo = scope.ServiceProvider.GetRequiredService<IResearchTopicRepository>();
        var paperRepo = scope.ServiceProvider.GetRequiredService<IResearchPaperRepository>();
        var qualityRepo = scope.ServiceProvider.GetRequiredService<IPaperQualityRepository>();
        var context = scope.ServiceProvider.GetRequiredService<ScholarTrendDbContext>();

        var topics = await topicRepo.GetAllAsync();

        foreach (var topic in topics)
        {
            if (ct.IsCancellationRequested) break;
            
            await AssessTopicPapersAsync(topic.Id, ct);
        }

        _logger.LogInformation("Paper quality assessment completed.");
    }

    public async Task AssessTopicPapersAsync(int topicId, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Assessing paper quality for topic {TopicId} (Top-{Sample} sample)...",
            topicId, SampleCoverageLevels.SampleTarget);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ScholarTrendDbContext>();
        var paperRepo = scope.ServiceProvider.GetRequiredService<IResearchPaperRepository>();

        var paperIds = await paperRepo.GetTopPaperIdsForTopicSampleAsync(
            topicId, SampleCoverageLevels.SampleTarget);

        var assessed = 0;
        var batch = new List<PaperQuality>();

        var existingQualityIds = await context.PaperQualities
            .AsNoTracking()
            .Where(q => paperIds.Contains(q.PaperId))
            .Select(q => q.PaperId)
            .ToListAsync(ct);
        var existingSet = existingQualityIds.ToHashSet();

        var pendingIds = paperIds.Where(id => !existingSet.Contains(id)).ToList();
        if (pendingIds.Count == 0)
        {
            _logger.LogInformation("Topic {TopicId}: Top sample quality already assessed.", topicId);
            return;
        }

        var papers = await context.ResearchPapers
            .Include(p => p.PaperAuthors)
            .Include(p => p.PaperKeywords)
            .Where(p => pendingIds.Contains(p.Id))
            .ToListAsync(ct);

        foreach (var paper in papers)
        {
            if (ct.IsCancellationRequested) break;

            var quality = AssessPaperQuality(paper, context, ct);
            batch.Add(quality);
            assessed++;

            if (batch.Count >= BatchSize)
            {
                await context.PaperQualities.AddRangeAsync(batch, ct);
                await context.SaveChangesAsync(ct);
                batch.Clear();
                _logger.LogInformation("Assessed quality for {Count} papers in topic {TopicId}...", assessed, topicId);
            }
        }

        if (batch.Count > 0)
        {
            await context.PaperQualities.AddRangeAsync(batch, ct);
            await context.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Completed assessing quality for {Count} papers in topic {TopicId}", assessed, topicId);
    }

    public async Task AssessPaperQualityAsync(int paperId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ScholarTrendDbContext>();

        var existingQuality = await context.PaperQualities
            .FirstOrDefaultAsync(q => q.PaperId == paperId, ct);

        if (existingQuality != null)
            return;

        var paper = await context.ResearchPapers
            .Include(p => p.PaperAuthors)
            .Include(p => p.PaperKeywords)
            .FirstOrDefaultAsync(p => p.Id == paperId, ct);

        if (paper == null)
            return;

        var quality = AssessPaperQuality(paper, context, ct);
        await context.PaperQualities.AddAsync(quality, ct);
        await context.SaveChangesAsync(ct);
    }

    private PaperQuality AssessPaperQuality(ResearchPaper paper, ScholarTrendDbContext context, CancellationToken ct)
    {
        var hasPdf = !string.IsNullOrWhiteSpace(paper.PdfUrl);
        var hasAbstract = !string.IsNullOrWhiteSpace(paper.Abstract);
        var abstractLength = paper.Abstract?.Length ?? 0;
        var hasDoi = !string.IsNullOrWhiteSpace(paper.Doi);
        var hasKeywords = paper.PaperKeywords?.Any() ?? false;
        var hasJournal = paper.JournalId.HasValue;
        var authorCount = paper.PaperAuthors?.Count ?? 0;
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

        return new PaperQuality
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
    }
}
