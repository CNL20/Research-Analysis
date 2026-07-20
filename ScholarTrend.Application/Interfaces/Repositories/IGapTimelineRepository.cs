using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface IGapTimelineRepository
{
    Task<List<GapTimeline>> GetByTopicIdAsync(int topicId);
    Task<List<GapTimeline>> GetByTopicAndYearAsync(int topicId, int year);
    Task UpsertAsync(GapTimeline timeline);
    Task UpsertManyAsync(List<GapTimeline> timelines);
    Task DeleteByTopicAsync(int topicId);
}
