using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.DTOs.Journals;
using ScholarTrend.Application.Interfaces;

namespace ScholarTrend.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class JournalsController : ControllerBase
{
    private readonly IJournalService _journalService;

    public JournalsController(IJournalService journalService)
    {
        _journalService = journalService;
    }

    /// <summary>
    /// Get all journals.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<JournalListItemDto>>>> GetAll()
    {
        var result = await _journalService.GetAllAsync();
        return Ok(ApiResponse<IReadOnlyList<JournalListItemDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Get journal details by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<JournalDetailDto>>> GetById(int id)
    {
        try
        {
            var result = await _journalService.GetByIdAsync(id);
            return Ok(ApiResponse<JournalDetailDto>.SuccessResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<JournalDetailDto>.FailResponse(ex.Message));
        }
    }
}
