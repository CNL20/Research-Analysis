using ScholarTrend.Application.DTOs.Topics;
using ScholarTrend.Application.DTOs.Common;

namespace ScholarTrend.Application.Interfaces;

public interface ITopicService
{
    Task<PagedResult<TopicListItemDto>> GetPagedAsync(string? keyword, int page, int pageSize);
    Task<TopicDetailDto> GetByIdAsync(int id);
}
