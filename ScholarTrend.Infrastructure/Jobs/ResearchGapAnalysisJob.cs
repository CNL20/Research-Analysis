using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.GapAnalysis;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Jobs;

public class ResearchGapAnalysisJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGapGenerationJobTracker _tracker;
    private readonly ILogger<ResearchGapAnalysisJob> _logger;

    public ResearchGapAnalysisJob(
        IServiceScopeFactory scopeFactory,
        IGapGenerationJobTracker tracker,
        ILogger<ResearchGapAnalysisJob> logger)
    {
        _scopeFactory = scopeFactory;
        _tracker = tracker;
        _logger = logger;
    }

    /// <summary>Hangfire entry used by topic API async generate (tracked job).</summary>
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 30)]
    public async Task GenerateGapsForTopicTrackedAsync(
        int topicId,
        string trackerJobId,
        bool force,
        CancellationToken ct = default)
    {
        _tracker.MarkRunning(trackerJobId);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var researchGapService = scope.ServiceProvider.GetRequiredService<IResearchGapService>();
            var report = await researchGapService.GenerateGapReportAsync(topicId, force, ct);
            _tracker.MarkCompleted(trackerJobId, report.Gaps.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tracked gap generation failed for topic {TopicId}", topicId);
            _tracker.MarkFailed(trackerJobId, ex.Message);
            throw;
        }
    }

    public async Task<ResearchGapReportDto?> GenerateGapsForTopicAsync(int topicId, CancellationToken ct = default)
    {
        _logger.LogInformation("Generating research gaps for topic {TopicId} (use cache if fresh)...", topicId);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var researchGapService = scope.ServiceProvider.GetRequiredService<IResearchGapService>();
            var report = await researchGapService.GenerateGapReportAsync(topicId, force: false, ct);
            _logger.LogInformation(
                "Gap step for topic {TopicId}: {GapCount} gaps, source={Source}, NeedsGeneration={Needs}, Stale={Stale}",
                topicId, report.Gaps.Count, report.Source, report.NeedsGeneration, report.IsStale);
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating research gaps for topic {TopicId}", topicId);
            throw;
        }
    }

    public async Task RunScheduledGenerationAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting scheduled research gap generation...");

        using var scope = _scopeFactory.CreateScope();
        var topicRepo = scope.ServiceProvider.GetRequiredService<IResearchTopicRepository>();

        var topics = await topicRepo.GetAllAsync();

        foreach (var topic in topics)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                _logger.LogInformation("Regenerating research gaps for topic {TopicId} ({TopicName})...",
                    topic.Id, topic.TopicName);

                await RegenerateGapsAsync(topic.Id, ct);

                _logger.LogInformation(
                    "Regenerated research gaps for topic {TopicId} ({TopicName}).",
                    topic.Id, topic.TopicName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error regenerating research gaps for topic {TopicId}", topic.Id);
            }
        }

        _logger.LogInformation("Scheduled research gap generation completed.");
    }

    public async Task RegenerateGapsAsync(int topicId, CancellationToken ct = default)
    {
        _logger.LogInformation("Regenerating research gaps for topic {TopicId}...", topicId);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ScholarTrendDbContext>();
        var researchGapService = scope.ServiceProvider.GetRequiredService<IResearchGapService>();

        var topic = await context.ResearchTopics.FindAsync(new object[] { topicId }, ct);
        if (topic == null)
        {
            _logger.LogWarning("Topic {TopicId} not found", topicId);
            return;
        }

        try
        {
            var report = await researchGapService.GenerateGapReportAsync(topicId, force: true, ct);
            _logger.LogInformation(
                "Regenerated {GapCount} research gaps for topic {TopicId}.",
                report.Gaps.Count,
                topicId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error regenerating research gaps for topic {TopicId}", topicId);
        }
    }
}
