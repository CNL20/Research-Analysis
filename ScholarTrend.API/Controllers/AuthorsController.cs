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
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuthorListItemDto>>>> GetAll()
    {
        var result = await _authorService.GetAllAsync();
        return Ok(ApiResponse<IReadOnlyList<AuthorListItemDto>>.SuccessResponse(result));
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
