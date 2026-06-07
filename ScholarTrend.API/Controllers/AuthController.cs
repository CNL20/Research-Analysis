using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarTrend.Application.DTOs.Auth;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.Interfaces;

namespace ScholarTrend.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Register a new user account. Default role: LecturerStudent.
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var result = await _authService.RegisterAsync(request);
            return Ok(ApiResponse<AuthResponse>.SuccessResponse(result, "Registration successful."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<AuthResponse>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Login with email and password. Returns JWT token.
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var result = await _authService.LoginAsync(request);
            return Ok(ApiResponse<AuthResponse>.SuccessResponse(result, "Login successful."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<AuthResponse>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Get current user profile. Requires authentication.
    /// </summary>
    [Authorize]
    [HttpGet("profile")]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetProfile()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(ApiResponse<UserProfileDto>.FailResponse("User not authenticated."));
            }

            var result = await _authService.GetProfileAsync(userId);
            return Ok(ApiResponse<UserProfileDto>.SuccessResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<UserProfileDto>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Update current user profile. Requires authentication.
    /// </summary>
    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(ApiResponse<UserProfileDto>.FailResponse("User not authenticated."));
            }

            var result = await _authService.UpdateProfileAsync(userId, request);
            return Ok(ApiResponse<UserProfileDto>.SuccessResponse(result, "Profile updated successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<UserProfileDto>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Exchange a valid refresh token for a new access token and refresh token pair.
    /// </summary>
    [HttpPost("refresh-token")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var result = await _authService.RefreshTokenAsync(request);
            return Ok(ApiResponse<AuthResponse>.SuccessResponse(result, "Token refreshed successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<AuthResponse>.FailResponse(ex.Message));
        }
    }
}
