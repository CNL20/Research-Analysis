using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.DTOs.Topics;
using ScholarTrend.Application.DTOs.TopicInsights;
using ScholarTrend.Application.Interfaces;

namespace ScholarTrend.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TopicsController : ControllerBase
{
    private readonly ITopicService _topicService;
    private readonly ITopicInsightService _topicInsightService;

    public TopicsController(ITopicService topicService, ITopicInsightService topicInsightService)
    {
        _topicService = topicService;
        _topicInsightService = topicInsightService;
    }

    /// <summary>
    /// Get all research topics.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TopicListItemDto>>>> GetAll()
    {
        var result = await _topicService.GetAllAsync();
        return Ok(ApiResponse<IReadOnlyList<TopicListItemDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Get research topic details by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<TopicDetailDto>>> GetById(int id)
    {
        try
        {
            var result = await _topicService.GetByIdAsync(id);
            return Ok(ApiResponse<TopicDetailDto>.SuccessResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<TopicDetailDto>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Get topic insights dashboard (mock data for now).
    /// </summary>

    [HttpGet("{id:int}/insights/dashboard")]
    public async Task<ActionResult<ApiResponse<TopicInsightDashboardDto>>> GetInsightsDashboard(int id)
    {
        try
        {
            var result = await _topicInsightService.GetTopicInsightDashboardAsync(id);
            return Ok(ApiResponse<TopicInsightDashboardDto>.SuccessResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<TopicInsightDashboardDto>.FailResponse(ex.Message));
        }
    }
}
