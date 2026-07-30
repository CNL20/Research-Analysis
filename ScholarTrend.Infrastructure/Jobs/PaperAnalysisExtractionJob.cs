using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.GapAnalysis;
using ScholarTrend.Application.DTOs.Pdf;
using ScholarTrend.Application.DTOs.TopicInsights;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Application.Services;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;
using System.Text.Json;

namespace ScholarTrend.Infrastructure.Jobs;

public class PaperAnalysisExtractionJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaperAnalysisExtractionJob> _logger;

    /// <summary>Same pool as gap sampling (Top N with abstract).</summary>
    private static int SampleTarget => SampleCoverageLevels.SampleTarget;

    /// <summary>Cap per pipeline/run so each click stays closer to ~20–40s of AI work.</summary>
    private const int MaxExtractPerRun = 3;

    /// <summary>Groq-friendly pause between papers (skipped after the last one).</summary>
    private const int DelayBetweenRequestsMs = 1000;

    public PaperAnalysisExtractionJob(
        IServiceScopeFactory scopeFactory,
        ILogger<PaperAnalysisExtractionJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task RunExtractionAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting paper analysis extraction job (Hybrid mode)...");

        using var scope = _scopeFactory.CreateScope();
        var topicRepo = scope.ServiceProvider.GetRequiredService<IResearchTopicRepository>();

        var topics = await topicRepo.GetAllAsync();

        foreach (var topic in topics)
        {
            if (ct.IsCancellationRequested) break;
            await ExtractForTopicAsync(topic.Id, ct);
        }

        _logger.LogInformation("Paper analysis extraction job completed.");
    }

    /// <summary>
    /// Extract analysis for Top-N sample papers missing PaperAnalysis.
    /// Returns how many papers were newly extracted (0 = fully cached / nothing to do).
    /// </summary>
    public async Task<int> ExtractForTopicAsync(int topicId, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Extracting analysis for topic {TopicId} (Top-{Sample} sample, Hybrid mode)...",
            topicId, SampleTarget);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ScholarTrendDbContext>();
        var aiService = scope.ServiceProvider.GetRequiredService<IAiExtractionService>();
        var paperRepo = scope.ServiceProvider.GetRequiredService<IResearchPaperRepository>();
        var pdfTextService = scope.ServiceProvider.GetRequiredService<PdfTextExtractionService>();
        var sectionExtractor = new SectionExtractor();

        var samplePaperIds = await paperRepo.GetTopPaperIdsForTopicSampleAsync(topicId, SampleTarget);
        if (samplePaperIds.Count == 0)
        {
            _logger.LogWarning("Topic {TopicId}: no papers in Top-{Sample} sample (need abstract).", topicId, SampleTarget);
            return 0;
        }

        var alreadyAnalyzed = await context.PaperAnalyses
            .AsNoTracking()
            .Where(a => samplePaperIds.Contains(a.PaperId))
            .Select(a => a.PaperId)
            .ToListAsync(ct);
        var alreadySet = alreadyAnalyzed.ToHashSet();

        var pendingIds = samplePaperIds.Where(id => !alreadySet.Contains(id)).ToList();

        _logger.LogInformation(
            "Topic {TopicId}: sample {SampleSize}, already analyzed {Analyzed}, pending extract {Pending}",
            topicId, samplePaperIds.Count, alreadySet.Count, pendingIds.Count);

        if (pendingIds.Count == 0)
        {
            _logger.LogInformation("Topic {TopicId}: Top sample fully extracted — nothing to do.", topicId);
            return 0;
        }

        var paperMap = await context.ResearchPapers
            .AsNoTracking()
            .Where(p => pendingIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var papers = pendingIds
            .Where(id => paperMap.ContainsKey(id))
            .Select(id => paperMap[id])
            .Take(MaxExtractPerRun)
            .ToList();

        if (pendingIds.Count > MaxExtractPerRun)
        {
            _logger.LogInformation(
                "Topic {TopicId}: extracting {Batch}/{Pending} pending papers this run (cap {Cap})",
                topicId, papers.Count, pendingIds.Count, MaxExtractPerRun);
        }

        var processed = 0;
        var failed = 0;
        var skippedNoAbstract = 0;
        var hybridUsed = 0;
        var abstractOnlyUsed = 0;

        for (var i = 0; i < papers.Count; i++)
        {
            if (ct.IsCancellationRequested) break;
            var paper = papers[i];

            try
            {
                var existingAnalysis = await context.PaperAnalyses
                    .FirstOrDefaultAsync(a => a.PaperId == paper.Id, ct);

                if (existingAnalysis != null)
                    continue;

                if (string.IsNullOrWhiteSpace(paper.Abstract))
                {
                    _logger.LogWarning("Paper {PaperId} has no abstract, skipping", paper.Id);
                    skippedNoAbstract++;
                    continue;
                }

                string? fullText = null;
                bool hasPdf = false;

                try
                {
                    var pdfResult = await pdfTextService.ExtractForPaperAsync(paper.Id, forceReExtract: false, ct);
                    if (pdfResult.Status == "Extracted" && !string.IsNullOrWhiteSpace(pdfResult.ExtractedText))
                    {
                        fullText = pdfResult.ExtractedText;
                        hasPdf = true;
                        _logger.LogDebug("Paper {PaperId}: Using parsed PDF text ({Chars} chars)", paper.Id, fullText.Length);
                    }
                    else
                    {
                        _logger.LogDebug("Paper {PaperId}: No parsed PDF text available", paper.Id);
                    }
                }
                catch (Exception pdfEx)
                {
                    _logger.LogWarning(pdfEx, "Failed to get PDF text for paper {PaperId}, will use abstract only", paper.Id);
                }

                ExtractedSectionsDto? sections = null;
                if (!string.IsNullOrWhiteSpace(fullText))
                {
                    sections = sectionExtractor.ExtractRelevantSections(fullText);
                }

                var hybridResult = await aiService.ExtractHybridAsync(
                    paper.Abstract,
                    sections?.Discussion,
                    sections?.Conclusion,
                    sections?.Introduction,
                    sections?.Methodology,
                    ct);

                if (hybridResult == null)
                {
                    _logger.LogWarning("Hybrid extraction failed for paper {PaperId}, falling back to abstract-only", paper.Id);
                    failed++;
                    continue;
                }

                var merged = hybridResult.MergedExtraction;

                var analysis = new PaperAnalysis
                {
                    PaperId = paper.Id,
                    ResearchProblem = merged.ResearchProblem,
                    Method = merged.Methods.FirstOrDefault(),
                    Dataset = merged.Datasets.FirstOrDefault(),
                    Metric = merged.Metric,
                    Contribution = merged.Contribution,
                    MethodsJson = JsonSerializer.Serialize(merged.Methods),
                    DatasetsJson = JsonSerializer.Serialize(merged.Datasets),
                    LimitationsJson = JsonSerializer.Serialize(merged.Limitations),
                    FutureWorkJson = JsonSerializer.Serialize(merged.FutureWork),
                    DiscussionsJson = JsonSerializer.Serialize(merged.Discussions),
                    ConclusionsJson = JsonSerializer.Serialize(merged.Conclusions),
                    Confidence = hybridResult.Metadata.ConfidenceBreakdown.OverallConfidence,
                    AnalysisLevel = hasPdf ? AnalysisLevels.FullText : AnalysisLevels.Abstract,
                    AnalysisSource = (hybridResult.Metadata.UsedDiscussion || hybridResult.Metadata.UsedConclusion)
                        ? ExtractionSource.Hybrid
                        : ExtractionSource.AbstractOnly,
                    UsedAbstract = hybridResult.Metadata.UsedAbstract,
                    UsedDiscussion = hybridResult.Metadata.UsedDiscussion,
                    UsedConclusion = hybridResult.Metadata.UsedConclusion,
                    AbstractConfidence = hybridResult.Metadata.ConfidenceBreakdown.AbstractConfidence,
                    DiscussionConfidence = hybridResult.Metadata.ConfidenceBreakdown.DiscussionConfidence,
                    ConclusionConfidence = hybridResult.Metadata.ConfidenceBreakdown.ConclusionConfidence,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    HybridMetadataJson = JsonSerializer.Serialize(hybridResult.Metadata)
                };

                await context.PaperAnalyses.AddAsync(analysis, ct);
                await context.SaveChangesAsync(ct);

                processed++;
                if (analysis.AnalysisSource == ExtractionSource.Hybrid)
                    hybridUsed++;
                else
                    abstractOnlyUsed++;

                _logger.LogInformation(
                    "Extracted analysis for paper {PaperId} ({Processed}/{Pending}) - Source: {Source}, Confidence: {Confidence}",
                    paper.Id, processed, papers.Count, analysis.AnalysisSource, analysis.Confidence);

                if (i < papers.Count - 1)
                    await Task.Delay(DelayBetweenRequestsMs, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting analysis for paper {PaperId}", paper.Id);
                failed++;
            }
        }

        _logger.LogInformation(
            "Completed extraction for topic {TopicId}: {Processed} processed, {Failed} failed, {Skipped} skipped (no abstract). Hybrid: {Hybrid}, Abstract-only: {AbstractOnly}. Sample coverage now ~{Analyzed}/{SampleSize}",
            topicId, processed, failed, skippedNoAbstract, hybridUsed, abstractOnlyUsed,
            alreadySet.Count + processed, samplePaperIds.Count);

        return processed;
    }
}
