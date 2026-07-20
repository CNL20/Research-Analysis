using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

public class GapTimelineRepository : IGapTimelineRepository
{
    private readonly DbSet<GapTimeline> _db;

    public GapTimelineRepository(ScholarTrendDbContext context)
    {
        _db = context.Set<GapTimeline>();
    }

    public async Task<List<GapTimeline>> GetByTopicIdAsync(int topicId)
    {
        return await _db
            .Where(x => x.TopicId == topicId)
            .OrderBy(x => x.Year)
            .ThenBy(x => x.GapType)
            .ToListAsync();
    }

    public async Task<List<GapTimeline>> GetByTopicAndYearAsync(int topicId, int year)
    {
        return await _db
            .Where(x => x.TopicId == topicId && x.Year == year)
            .ToListAsync();
    }

    public async Task UpsertAsync(GapTimeline timeline)
    {
        var existing = await _db.FirstOrDefaultAsync(x =>
            x.TopicId == timeline.TopicId &&
            x.Year == timeline.Year &&
            x.GapType == timeline.GapType &&
            x.GapTitle == timeline.GapTitle);

        if (existing != null)
        {
            existing.PaperCount = timeline.PaperCount;
            existing.IsResolved = timeline.IsResolved;
            existing.ResolvedInYear = timeline.ResolvedInYear;
            existing.Trend = timeline.Trend;
            existing.TrackedAt = timeline.TrackedAt;
        }
        else
        {
            await _db.AddAsync(timeline);
        }
    }

    public async Task UpsertManyAsync(List<GapTimeline> timelines)
    {
        foreach (var timeline in timelines)
        {
            await UpsertAsync(timeline);
        }
    }

    public async Task DeleteByTopicAsync(int topicId)
    {
        await _db.Where(x => x.TopicId == topicId).ExecuteDeleteAsync();
    }
}
