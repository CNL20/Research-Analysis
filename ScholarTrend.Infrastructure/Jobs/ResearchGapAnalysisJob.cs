using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Application.Services;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Jobs;

public class ResearchGapAnalysisJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ResearchGapAnalysisJob> _logger;

    public ResearchGapAnalysisJob(
        IServiceScopeFactory scopeFactory,
        ILogger<ResearchGapAnalysisJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task GenerateGapsForTopicAsync(int topicId, CancellationToken ct = default)
    {
        _logger.LogInformation("Generating research gaps for topic {TopicId}...", topicId);

        try
        {
            // RegenerateGapsAsync deletes existing gaps for the topic first to avoid duplicates
            // when this job runs on a schedule.
            await RegenerateGapsAsync(topicId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating research gaps for topic {TopicId}", topicId);
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

                // RegenerateGapsAsync handles deletion of existing gaps + generation in one place,
                // preventing the duplicate-gap problem that occurred when this job ran repeatedly.
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
        var gapRepo = scope.ServiceProvider.GetRequiredService<IResearchGapRepository>();

        var topic = await context.ResearchTopics.FindAsync(new object[] { topicId }, ct);
        if (topic == null)
        {
            _logger.LogWarning("Topic {TopicId} not found", topicId);
            return;
        }

        await gapRepo.DeleteByTopicAsync(topicId);
        await context.SaveChangesAsync(ct);

        try
        {
            var report = await researchGapService.GenerateGapReportAsync(topicId, ct);
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
