using ScholarTrend.Application.DTOs.TopicInsights;

namespace ScholarTrend.Application.Interfaces;

public interface ITopicInsightService
{
    Task<TopicInsightDashboardDto> GetTopicInsightDashboardAsync(int topicId);
}
