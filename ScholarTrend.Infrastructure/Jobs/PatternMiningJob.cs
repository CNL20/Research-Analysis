using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.GapAnalysis;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Application.Services;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Jobs;

public class PatternMiningJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PatternMiningJob> _logger;

    public PatternMiningJob(
        IServiceScopeFactory scopeFactory,
        ILogger<PatternMiningJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task MineAllTopicsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting pattern mining for all topics...");

        using var scope = _scopeFactory.CreateScope();
        var topicRepo = scope.ServiceProvider.GetRequiredService<IResearchTopicRepository>();
        var patternMiningService = scope.ServiceProvider.GetRequiredService<IPatternMiningService>();

        var topics = await topicRepo.GetAllAsync();

        foreach (var topic in topics)
        {
            if (ct.IsCancellationRequested) break;
            await MineTopicPatternsAsync(topic.Id, ct);
        }

        _logger.LogInformation("Pattern mining completed for all topics.");
    }

    public async Task MineTopicPatternsAsync(int topicId, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Mining patterns for topic {TopicId} (Top-{Sample} sample)...",
            topicId, SampleCoverageLevels.SampleTarget);

        using var scope = _scopeFactory.CreateScope();
        var patternMiningService = scope.ServiceProvider.GetRequiredService<IPatternMiningService>();
        var paperRepo = scope.ServiceProvider.GetRequiredService<IResearchPaperRepository>();

        try
        {
            var sampleIds = await paperRepo.GetTopPaperIdsForTopicSampleAsync(
                topicId, SampleCoverageLevels.SampleTarget);

            var result = sampleIds.Count > 0
                ? await patternMiningService.MinePatternsForPaperIdsAsync(topicId, sampleIds, ct)
                : await patternMiningService.MinePatternsAsync(topicId, ct);

            _logger.LogInformation(
                "Mined patterns for topic {TopicId}: {MethodCount} methods, {DatasetCount} datasets, {LimitationCount} limitations (sample {SampleSize})",
                topicId,
                result.Methods.Count,
                result.Datasets.Count,
                result.Limitations.Count,
                sampleIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mining patterns for topic {TopicId}", topicId);
        }
    }

    public async Task MineWithUpdatesAsync(int topicId, CancellationToken ct = default)
    {
        _logger.LogInformation("Mining patterns with updates for topic {TopicId}...", topicId);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ScholarTrendDbContext>();
        var patternMiningService = scope.ServiceProvider.GetRequiredService<IPatternMiningService>();

        var topic = await context.ResearchTopics.FindAsync(new object[] { topicId }, ct);
        if (topic == null)
        {
            _logger.LogWarning("Topic {TopicId} not found", topicId);
            return;
        }

        var patternRepo = scope.ServiceProvider.GetRequiredService<IPatternRepository>();
        await patternRepo.DeleteByTopicAsync(topicId);
        await context.SaveChangesAsync(ct);

        var result = await patternMiningService.MinePatternsAsync(topicId, ct);
        _logger.LogInformation(
            "Mined patterns for topic {TopicId}: {MethodCount} methods, {DatasetCount} datasets, {LimitationCount} limitations",
            topicId,
            result.Methods.Count,
            result.Datasets.Count,
            result.Limitations.Count);
    }
}
