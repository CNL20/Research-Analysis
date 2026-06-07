using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.DTOs.Papers;
using ScholarTrend.Application.Interfaces;

namespace ScholarTrend.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PapersController : ControllerBase
{
    private readonly IPaperService _paperService;

    public PapersController(IPaperService paperService)
    {
        _paperService = paperService;
    }

    /// <summary>
    /// Search research papers by keyword, author, or journal with optional filters.
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<ApiResponse<PagedResult<PaperListItemDto>>>> Search([FromQuery] PaperSearchRequest request)
    {
        var userId = GetUserId();
        var result = await _paperService.SearchAsync(request, userId);
        return Ok(ApiResponse<PagedResult<PaperListItemDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Get paper details by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<PaperDetailDto>>> GetById(int id)
    {
        try
        {
            var result = await _paperService.GetByIdAsync(id, GetUserId());
            return Ok(ApiResponse<PaperDetailDto>.SuccessResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<PaperDetailDto>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Get papers by research topic with pagination.
    /// </summary>
    [HttpGet("by-topic/{topicId:int}")]
    public async Task<ActionResult<ApiResponse<PagedResult<PaperListItemDto>>>> GetByTopic(
        int topicId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var result = await _paperService.GetByTopicAsync(topicId, page, pageSize);
            return Ok(ApiResponse<PagedResult<PaperListItemDto>>.SuccessResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<PagedResult<PaperListItemDto>>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Get papers by journal with pagination.
    /// </summary>
    [HttpGet("by-journal/{journalId:int}")]
    public async Task<ActionResult<ApiResponse<PagedResult<PaperListItemDto>>>> GetByJournal(
        int journalId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var result = await _paperService.GetByJournalAsync(journalId, page, pageSize);
            return Ok(ApiResponse<PagedResult<PaperListItemDto>>.SuccessResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<PagedResult<PaperListItemDto>>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Get recent search history for the current user.
    /// </summary>
    [HttpGet("search-history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SearchHistoryDto>>>> GetSearchHistory([FromQuery] int limit = 20)
    {
        var result = await _paperService.GetSearchHistoryAsync(GetUserId(), limit);
        return Ok(ApiResponse<IReadOnlyList<SearchHistoryDto>>.SuccessResponse(result));
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User not authenticated.");
    }
}
