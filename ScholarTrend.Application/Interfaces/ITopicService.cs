using ScholarTrend.Application.DTOs.Topics;

namespace ScholarTrend.Application.Interfaces;

public interface ITopicService
{
    Task<IReadOnlyList<TopicListItemDto>> GetAllAsync();
    Task<TopicDetailDto> GetByIdAsync(int id);
}
