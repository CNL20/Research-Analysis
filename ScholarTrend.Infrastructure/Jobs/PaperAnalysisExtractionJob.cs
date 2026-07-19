using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.Interfaces.Repositories;
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
        _logger.LogInformation("Starting paper analysis extraction job...");

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
        _logger.LogInformation("Extracting analysis for topic {TopicId}...", topicId);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ScholarTrendDbContext>();
        var aiService = scope.ServiceProvider.GetRequiredService<IAiExtractionService>();
        var paperRepo = scope.ServiceProvider.GetRequiredService<IResearchPaperRepository>();

        var papers = await paperRepo.GetPapersByTopicAsync(topicId, BatchSize);
        var processed = 0;
        var failed = 0;

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
                    continue;
                }

                var extraction = await aiService.ExtractFromAbstractAsync(paper.Abstract, ct);
                if (extraction == null)
                {
                    _logger.LogWarning("Failed to extract from paper {PaperId}", paper.Id);
                    failed++;
                    continue;
                }

                var quality = await context.PaperQualities
                    .FirstOrDefaultAsync(q => q.PaperId == paper.Id, ct);

                var analysis = new PaperAnalysis
                {
                    PaperId = paper.Id,
                    ResearchProblem = extraction.ResearchProblem,
                    Method = extraction.Methods.FirstOrDefault(),
                    Dataset = extraction.Datasets.FirstOrDefault(),
                    Metric = extraction.Metric,
                    Contribution = extraction.Contribution,
                    MethodsJson = JsonSerializer.Serialize(extraction.Methods),
                    DatasetsJson = JsonSerializer.Serialize(extraction.Datasets),
                    LimitationsJson = JsonSerializer.Serialize(extraction.Limitations),
                    FutureWorkJson = JsonSerializer.Serialize(extraction.FutureWork),
                    DiscussionsJson = JsonSerializer.Serialize(extraction.Discussions),
                    ConclusionsJson = JsonSerializer.Serialize(extraction.Conclusions),
                    Confidence = CalculateConfidence(extraction),
                    AnalysisLevel = quality?.HasPdf == true ? AnalysisLevels.Abstract : AnalysisLevels.Abstract,
                    AnalysisSource = "Groq",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await context.PaperAnalyses.AddAsync(analysis, ct);
                await context.SaveChangesAsync(ct);

                processed++;
                _logger.LogInformation("Extracted analysis for paper {PaperId} ({Processed}/{BatchSize})", paper.Id, processed, BatchSize);

                await Task.Delay(DelayBetweenRequests, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting analysis for paper {PaperId}", paper.Id);
                failed++;
            }
        }

        _logger.LogInformation("Completed extraction for topic {TopicId}: {Processed} processed, {Failed} failed", 
            topicId, processed, failed);
    }

    private int CalculateConfidence(Application.DTOs.TopicInsights.AiPaperExtractionDto extraction)
    {
        int score = 50;
        if (extraction.Methods.Any()) score += 10;
        if (extraction.Datasets.Any()) score += 10;
        if (extraction.Limitations.Any()) score += 10;
        if (extraction.FutureWork.Any()) score += 10;
        if (extraction.ResearchProblem != null) score += 5;
        if (extraction.Metric != null) score += 5;
        return Math.Min(score, 100);
    }
}
