using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarTrend.Application.DTOs.Auth;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Domain.Constants;

namespace ScholarTrend.API.Controllers;

[Authorize(Roles = RoleConstants.Admin)]
[ApiController]
[Route("api/admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly IUserService _userService;

    public AdminUsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// List all users with optional filters. Admin only.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserListItemDto>>>> GetUsers(
        [FromQuery] UserFilterRequest filter)
    {
        var users = await _userService.GetUsersAsync(filter);
        return Ok(ApiResponse<IReadOnlyList<UserListItemDto>>.SuccessResponse(users));
    }

    /// <summary>
    /// Get user details by ID. Admin only.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<UserListItemDto>>> GetUser(string id)
    {
        try
        {
            var user = await _userService.GetUserByIdAsync(id);
            return Ok(ApiResponse<UserListItemDto>.SuccessResponse(user));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<UserListItemDto>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Activate or deactivate a user account. Admin only.
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<ActionResult<ApiResponse<UserListItemDto>>> UpdateStatus(
        string id,
        [FromBody] UpdateUserStatusRequest request)
    {
        try
        {
            var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var user = await _userService.UpdateUserStatusAsync(id, request, adminUserId);
            return Ok(ApiResponse<UserListItemDto>.SuccessResponse(user, "User status updated."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<UserListItemDto>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Change a user's role. Admin only.
    /// </summary>
    [HttpPatch("{id}/role")]
    public async Task<ActionResult<ApiResponse<UserListItemDto>>> UpdateRole(
        string id,
        [FromBody] UpdateUserRoleRequest request)
    {
        try
        {
            var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var user = await _userService.UpdateUserRoleAsync(id, request, adminUserId);
            return Ok(ApiResponse<UserListItemDto>.SuccessResponse(user, "User role updated."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<UserListItemDto>.FailResponse(ex.Message));
        }
    }
}
