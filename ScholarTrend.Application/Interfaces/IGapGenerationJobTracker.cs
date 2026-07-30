using ScholarTrend.Application.DTOs.GapAnalysis;

namespace ScholarTrend.Application.Interfaces;

/// <summary>
/// Tracks async research-gap / pipeline jobs (Hangfire) for status polling.
/// </summary>
public interface IGapGenerationJobTracker
{
    GapGenerationJobDto Register(int topicId, string? hangfireJobId = null);
    void MarkRunning(string jobId);
    void MarkProgress(string jobId, string message);
    void MarkCompleted(string jobId, int gapCount, string? message = null);
    void MarkFailed(string jobId, string message);
    GapGenerationJobDto? Get(string jobId);
    GapGenerationJobDto? GetLatestForTopic(int topicId);
}
