using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    private const int BatchSize = 10;
    private const int DelayBetweenRequests = 4000;

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
        var context = scope.ServiceProvider.GetRequiredService<ScholarTrendDbContext>();
        var topicRepo = scope.ServiceProvider.GetRequiredService<IResearchTopicRepository>();

        var topics = await topicRepo.GetAllAsync();

        foreach (var topic in topics)
        {
            if (ct.IsCancellationRequested) break;
            await ExtractForTopicAsync(topic.Id, ct);
        }

        _logger.LogInformation("Paper analysis extraction job completed.");
    }

    public async Task ExtractForTopicAsync(int topicId, CancellationToken ct = default)
    {
        _logger.LogInformation("Extracting analysis for topic {TopicId} (Hybrid mode)...", topicId);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ScholarTrendDbContext>();
        var aiService = scope.ServiceProvider.GetRequiredService<IAiExtractionService>();
        var paperRepo = scope.ServiceProvider.GetRequiredService<IResearchPaperRepository>();
        var pdfTextService = scope.ServiceProvider.GetRequiredService<PdfTextExtractionService>();
        var sectionExtractor = new SectionExtractor();

        var papers = await paperRepo.GetPapersByTopicAsync(topicId, BatchSize);
        var processed = 0;
        var failed = 0;
        var skippedNoAbstract = 0;
        var hybridUsed = 0;
        var abstractOnlyUsed = 0;

        foreach (var paper in papers)
        {
            if (ct.IsCancellationRequested) break;

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

                // Get full text from parsed PDF (if available)
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

                // Extract sections from full text
                ExtractedSectionsDto? sections = null;
                if (!string.IsNullOrWhiteSpace(fullText))
                {
                    sections = sectionExtractor.ExtractRelevantSections(fullText);
                }

                // Perform hybrid extraction
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

                var quality = await context.PaperQualities
                    .FirstOrDefaultAsync(q => q.PaperId == paper.Id, ct);

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
                    UpdatedAt = DateTime.UtcNow
                };

                // Store hybrid metadata as JSON
                analysis.HybridMetadataJson = JsonSerializer.Serialize(hybridResult.Metadata);

                await context.PaperAnalyses.AddAsync(analysis, ct);
                await context.SaveChangesAsync(ct);

                processed++;
                if (analysis.AnalysisSource == ExtractionSource.Hybrid)
                    hybridUsed++;
                else
                    abstractOnlyUsed++;

                _logger.LogInformation(
                    "Extracted analysis for paper {PaperId} ({Processed}/{BatchSize}) - Source: {Source}, Confidence: {Confidence}",
                    paper.Id, processed, BatchSize, analysis.AnalysisSource, analysis.Confidence);

                await Task.Delay(DelayBetweenRequests, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting analysis for paper {PaperId}", paper.Id);
                failed++;
            }
        }

        _logger.LogInformation(
            "Completed extraction for topic {TopicId}: {Processed} processed, {Failed} failed, {Skipped} skipped (no abstract). Hybrid: {Hybrid}, Abstract-only: {AbstractOnly}",
            topicId, processed, failed, skippedNoAbstract, hybridUsed, abstractOnlyUsed);
    }
}
