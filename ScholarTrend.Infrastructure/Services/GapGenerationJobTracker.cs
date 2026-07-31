using System.Collections.Concurrent;
using ScholarTrend.Application.DTOs.GapAnalysis;
using ScholarTrend.Application.Interfaces;

namespace ScholarTrend.Infrastructure.Services;

public class GapGenerationJobTracker : IGapGenerationJobTracker
{
    private readonly ConcurrentDictionary<string, GapGenerationJobDto> _jobs = new();
    private readonly ConcurrentDictionary<int, string> _latestByTopic = new();

    public GapGenerationJobDto Register(int topicId, string? hangfireJobId = null)
    {
        var jobId = hangfireJobId ?? Guid.NewGuid().ToString("N");
        var dto = new GapGenerationJobDto
        {
            JobId = jobId,
            TopicId = topicId,
            Status = GapGenerationStatuses.Queued,
            Message = "Queued for background generation",
            CreatedAt = DateTime.UtcNow
        };

        _jobs[jobId] = dto;
        _latestByTopic[topicId] = jobId;
        return dto;
    }

    public void MarkRunning(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = GapGenerationStatuses.Running;
            job.Message = "Running...";
        }
    }

    public void MarkProgress(string jobId, string message)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = GapGenerationStatuses.Running;
            job.Message = message;
        }
    }

    public void MarkCompleted(string jobId, int gapCount, string? message = null)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = GapGenerationStatuses.Completed;
            job.GapCount = gapCount;
            job.Message = message ?? $"Generated {gapCount} gaps";
            job.CompletedAt = DateTime.UtcNow;
        }
    }

    public void MarkFailed(string jobId, string message)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = GapGenerationStatuses.Failed;
            job.Message = message;
            job.CompletedAt = DateTime.UtcNow;
        }
    }

    public GapGenerationJobDto? Get(string jobId) =>
        _jobs.TryGetValue(jobId, out var job) ? Clone(job) : null;

    public GapGenerationJobDto? GetLatestForTopic(int topicId) =>
        _latestByTopic.TryGetValue(topicId, out var jobId) ? Get(jobId) : null;

    private static GapGenerationJobDto Clone(GapGenerationJobDto job) => new()
    {
        JobId = job.JobId,
        TopicId = job.TopicId,
        Status = job.Status,
        Message = job.Message,
        GapCount = job.GapCount,
        CreatedAt = job.CreatedAt,
        CompletedAt = job.CompletedAt
    };
}
