using ScholarTrend.Application.DTOs.Topics;
using ScholarTrend.Application.DTOs.Trends;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Mappings;
using ScholarTrend.Application.DTOs.Common;

namespace ScholarTrend.Application.Services;

public class TopicService : ITopicService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITrendService _trendService;

    public TopicService(IUnitOfWork unitOfWork, ITrendService trendService)
    {
        _unitOfWork = unitOfWork;
        _trendService = trendService;
    }

    public async Task<PagedResult<TopicListItemDto>> GetPagedAsync(string? keyword, int page, int pageSize)
    {
        var (topics, totalCount) = await _unitOfWork.Topics.GetPagedAsync(keyword, page, pageSize);
        
        var topicIds = topics.Select(t => t.Id).ToList();
        var paperCounts = await _unitOfWork.ResearchPapers.CountByTopicIdsAsync(topicIds);

        var items = new List<TopicListItemDto>();
        foreach (var topic in topics)
        {
            items.Add(new TopicListItemDto
            {
                Id = topic.Id,
                TopicName = topic.TopicName,
                Description = topic.Description,
                PaperCount = paperCounts.GetValueOrDefault(topic.Id, 0)
            });
        }

        return new PagedResult<TopicListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<TopicDetailDto> GetByIdAsync(int id)
    {
        var topic = await _unitOfWork.Topics.GetByIdAsync(id);
        if (topic == null)
        {
            throw new InvalidOperationException("Topic not found.");
        }

        var paperCount = await _unitOfWork.ResearchPapers.CountByTopicAsync(id);
        var recentPapers = await _unitOfWork.ResearchPapers.GetPapersByTopicAsync(id, limit: 5);
        var trendSeries = await _trendService.GetTopicTrendsAsync(new TrendFilterRequest { TopicId = id });

        return new TopicDetailDto
        {
            Id = topic.Id,
            TopicName = topic.TopicName,
            Description = topic.Description,
            PaperCount = paperCount,
            RecentPapers = recentPapers.Select(PaperMapper.ToListItem).ToList(),
            TrendChart = trendSeries.FirstOrDefault()?.DataPoints ?? []
        };
    }
}
