using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarTrend.Application.DTOs.Bookmarks;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.Interfaces;

namespace ScholarTrend.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class BookmarksController : ControllerBase
{
    private readonly IBookmarkService _bookmarkService;

    public BookmarksController(IBookmarkService bookmarkService)
    {
        _bookmarkService = bookmarkService;
    }

    /// <summary>
    /// Get all bookmarks for the current user.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BookmarkDto>>>> GetBookmarks()
    {
        var result = await _bookmarkService.GetUserBookmarksAsync(GetUserId());
        return Ok(ApiResponse<IReadOnlyList<BookmarkDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Bookmark a research paper.
    /// </summary>
    [HttpPost("{paperId:int}")]
    public async Task<ActionResult<ApiResponse<BookmarkDto>>> AddBookmark(int paperId)
    {
        try
        {
            var result = await _bookmarkService.AddBookmarkAsync(GetUserId(), paperId);
            return Ok(ApiResponse<BookmarkDto>.SuccessResponse(result, "Paper bookmarked successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<BookmarkDto>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Remove a bookmarked paper.
    /// </summary>
    [HttpDelete("{paperId:int}")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveBookmark(int paperId)
    {
        try
        {
            await _bookmarkService.RemoveBookmarkAsync(GetUserId(), paperId);
            return Ok(ApiResponse<object>.SuccessResponse(new { }, "Bookmark removed successfully."));
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
