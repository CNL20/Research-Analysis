using ScholarTrend.Application.DTOs.Topics;
using ScholarTrend.Application.DTOs.Trends;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Mappings;

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

    public async Task<IReadOnlyList<TopicListItemDto>> GetAllAsync()
    {
        var topics = await _unitOfWork.Topics.GetAllAsync();
        
        var topicIds = topics.Select(t => t.Id).ToList();
        var paperCounts = await _unitOfWork.ResearchPapers.CountByTopicIdsAsync(topicIds);

        var result = new List<TopicListItemDto>();
        foreach (var topic in topics)
        {
            result.Add(new TopicListItemDto
            {
                Id = topic.Id,
                TopicName = topic.TopicName,
                Description = topic.Description,
                PaperCount = paperCounts.GetValueOrDefault(topic.Id, 0)
            });
        }

        return result;
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
