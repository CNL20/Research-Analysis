using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarTrend.Application.DTOs.Authors;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.Interfaces;

namespace ScholarTrend.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AuthorsController : ControllerBase
{
    private readonly IAuthorService _authorService;

    public AuthorsController(IAuthorService authorService)
    {
        _authorService = authorService;
    }

    /// <summary>
    /// Get all authors.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AuthorListItemDto>>>> GetAll(
        [FromQuery] string? keyword, 
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10)
    {
        var result = await _authorService.GetPagedAsync(keyword, page, pageSize);
        return Ok(ApiResponse<PagedResult<AuthorListItemDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Resolve an author by exact name and return profile with ID.
    /// </summary>
    [HttpGet("by-name")]
    public async Task<ActionResult<ApiResponse<AuthorDetailDto>>> GetByName([FromQuery] string name)
    {
        try
        {
            var result = await _authorService.GetByNameAsync(name);
            return Ok(ApiResponse<AuthorDetailDto>.SuccessResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<AuthorDetailDto>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Get author details by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<AuthorDetailDto>>> GetById(int id)
    {
        try
        {
            var result = await _authorService.GetByIdAsync(id);
            return Ok(ApiResponse<AuthorDetailDto>.SuccessResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<AuthorDetailDto>.FailResponse(ex.Message));
        }
    }
}
