using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.DTOs.Follows;
using ScholarTrend.Application.Interfaces;

namespace ScholarTrend.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FollowsController : ControllerBase
{
    private readonly IFollowService _followService;

    public FollowsController(IFollowService followService)
    {
        _followService = followService;
    }

    [HttpGet("topics")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FollowItemDto>>>> GetFollowedTopics()
    {
        var result = await _followService.GetFollowedTopicsAsync(GetUserId());
        return Ok(ApiResponse<IReadOnlyList<FollowItemDto>>.SuccessResponse(result));
    }

    [HttpGet("journals")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FollowItemDto>>>> GetFollowedJournals()
    {
        var result = await _followService.GetFollowedJournalsAsync(GetUserId());
        return Ok(ApiResponse<IReadOnlyList<FollowItemDto>>.SuccessResponse(result));
    }

    [HttpPost("topics/{topicId:int}")]
    public async Task<ActionResult<ApiResponse<FollowItemDto>>> FollowTopic(int topicId)
    {
        try
        {
            var result = await _followService.FollowTopicAsync(GetUserId(), topicId);
            return Ok(ApiResponse<FollowItemDto>.SuccessResponse(result, "Topic followed successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<FollowItemDto>.FailResponse(ex.Message));
        }
    }

    [HttpDelete("topics/{topicId:int}")]
    public async Task<ActionResult<ApiResponse<object>>> UnfollowTopic(int topicId)
    {
        try
        {
            await _followService.UnfollowTopicAsync(GetUserId(), topicId);
            return Ok(ApiResponse<object>.SuccessResponse(new { }, "Topic unfollowed successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<object>.FailResponse(ex.Message));
        }
    }

    [HttpPost("journals/{journalId:int}")]
    public async Task<ActionResult<ApiResponse<FollowItemDto>>> FollowJournal(int journalId)
    {
        try
        {
            var result = await _followService.FollowJournalAsync(GetUserId(), journalId);
            return Ok(ApiResponse<FollowItemDto>.SuccessResponse(result, "Journal followed successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<FollowItemDto>.FailResponse(ex.Message));
        }
    }

    [HttpDelete("journals/{journalId:int}")]
    public async Task<ActionResult<ApiResponse<object>>> UnfollowJournal(int journalId)
    {
        try
        {
            await _followService.UnfollowJournalAsync(GetUserId(), journalId);
            return Ok(ApiResponse<object>.SuccessResponse(new { }, "Journal unfollowed successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<object>.FailResponse(ex.Message));
        }
    }

    [HttpGet("authors")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FollowItemDto>>>> GetFollowedAuthors()
    {
        var result = await _followService.GetFollowedAuthorsAsync(GetUserId());
        return Ok(ApiResponse<IReadOnlyList<FollowItemDto>>.SuccessResponse(result));
    }

    [HttpGet("papers")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FollowItemDto>>>> GetFollowedPapers()
    {
        var result = await _followService.GetFollowedPapersAsync(GetUserId());
        return Ok(ApiResponse<IReadOnlyList<FollowItemDto>>.SuccessResponse(result));
    }

    [HttpPost("authors/{authorId:int}")]
    public async Task<ActionResult<ApiResponse<FollowItemDto>>> FollowAuthor(int authorId)
    {
        try
        {
            var result = await _followService.FollowAuthorAsync(GetUserId(), authorId);
            return Ok(ApiResponse<FollowItemDto>.SuccessResponse(result, "Author followed successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<FollowItemDto>.FailResponse(ex.Message));
        }
    }

    [HttpDelete("authors/{authorId:int}")]
    public async Task<ActionResult<ApiResponse<object>>> UnfollowAuthor(int authorId)
    {
        try
        {
            await _followService.UnfollowAuthorAsync(GetUserId(), authorId);
            return Ok(ApiResponse<object>.SuccessResponse(new { }, "Author unfollowed successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<object>.FailResponse(ex.Message));
        }
    }

    [HttpPost("papers/{paperId:int}")]
    public async Task<ActionResult<ApiResponse<FollowItemDto>>> FollowPaper(int paperId)
    {
        try
        {
            var result = await _followService.FollowPaperAsync(GetUserId(), paperId);
            return Ok(ApiResponse<FollowItemDto>.SuccessResponse(result, "Paper followed successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<FollowItemDto>.FailResponse(ex.Message));
        }
    }

    [HttpDelete("papers/{paperId:int}")]
    public async Task<ActionResult<ApiResponse<object>>> UnfollowPaper(int paperId)
    {
        try
        {
            await _followService.UnfollowPaperAsync(GetUserId(), paperId);
            return Ok(ApiResponse<object>.SuccessResponse(new { }, "Paper unfollowed successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<object>.FailResponse(ex.Message));
        }
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User not authenticated.");
    }
}
