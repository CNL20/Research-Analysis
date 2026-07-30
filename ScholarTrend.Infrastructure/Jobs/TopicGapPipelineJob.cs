using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.Interfaces;

namespace ScholarTrend.Infrastructure.Jobs;

/// <summary>
/// Admin full gap pipeline as a Hangfire background job (quality → extract → pattern → gaps).
/// </summary>
public class TopicGapPipelineJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGapGenerationJobTracker _tracker;
    private readonly ILogger<TopicGapPipelineJob> _logger;

    public TopicGapPipelineJob(
        IServiceScopeFactory scopeFactory,
        IGapGenerationJobTracker tracker,
        ILogger<TopicGapPipelineJob> logger)
    {
        _scopeFactory = scopeFactory;
        _tracker = tracker;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
    public async Task RunTrackedAsync(int topicId, string trackerJobId, CancellationToken ct = default)
    {
        _tracker.MarkRunning(trackerJobId);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var qualityJob = scope.ServiceProvider.GetRequiredService<PaperQualityAssessmentJob>();
            var extractionJob = scope.ServiceProvider.GetRequiredService<PaperAnalysisExtractionJob>();
            var patternJob = scope.ServiceProvider.GetRequiredService<PatternMiningJob>();
            var gapJob = scope.ServiceProvider.GetRequiredService<ResearchGapAnalysisJob>();

            _tracker.MarkProgress(trackerJobId, "Step 1/4: Quality assessment...");
            await qualityJob.AssessTopicPapersAsync(topicId, ct);

            _tracker.MarkProgress(trackerJobId, "Step 2/4: Extracting paper analyses...");
            var extracted = await extractionJob.ExtractForTopicAsync(topicId, ct);

            if (extracted > 0)
            {
                _tracker.MarkProgress(trackerJobId, $"Step 3/4: Mining patterns ({extracted} new analyses)...");
                await patternJob.MineTopicPatternsAsync(topicId, ct);
            }
            else
            {
                _tracker.MarkProgress(trackerJobId, "Step 3/4: Skip pattern remine (no new extracts)");
            }

            _tracker.MarkProgress(trackerJobId, "Step 4/4: Generating research gaps...");
            var report = await gapJob.GenerateGapsForTopicAsync(topicId, ct);
            sw.Stop();

            var gapCount = report?.Gaps.Count ?? 0;
            var source = report?.Source ?? "unknown";
            _tracker.MarkCompleted(
                trackerJobId,
                gapCount,
                $"Done in {sw.Elapsed.TotalSeconds:F1}s — extracted={extracted}, gapSource={source}, gaps={gapCount}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline job failed for topic {TopicId}", topicId);
            _tracker.MarkFailed(trackerJobId, ex.Message);
            throw;
        }
    }
}
