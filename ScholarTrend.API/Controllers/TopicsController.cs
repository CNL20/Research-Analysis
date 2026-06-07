using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.DTOs.Topics;
using ScholarTrend.Application.Interfaces;

namespace ScholarTrend.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TopicsController : ControllerBase
{
    private readonly ITopicService _topicService;

    public TopicsController(ITopicService topicService)
    {
        _topicService = topicService;
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
}
