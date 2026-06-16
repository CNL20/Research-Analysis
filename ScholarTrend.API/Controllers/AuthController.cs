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
    private readonly IConfiguration _configuration;

    public AuthController(IAuthService authService, IConfiguration configuration)
    {
        _authService = authService;
        _configuration = configuration;
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
    /// Verify email using token. Called by Frontend.
    /// </summary>
    [HttpPost("verify-email")]
    public async Task<ActionResult<ApiResponse<bool>>> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        try
        {
            var result = await _authService.VerifyEmailAsync(request);
            return Ok(ApiResponse<bool>.SuccessResponse(result, "Email verified successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<bool>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Resend verification email.
    /// </summary>
    [HttpPost("resend-verification")]
    public async Task<ActionResult<ApiResponse<bool>>> ResendVerification([FromBody] ResendVerifyEmailRequest request)
    {
        try
        {
            // Lấy clientUrl từ cấu hình hoặc fallback
            var clientUrl = _configuration["ClientSettings:ClientUrl"] ?? "http://localhost:5173";
            var result = await _authService.ResendVerificationEmailAsync(request, clientUrl);
            return Ok(ApiResponse<bool>.SuccessResponse(result, "Verification email sent successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<bool>.FailResponse(ex.Message));
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
    /// Login with Google id_token. Returns JWT token.
    /// </summary>
    [HttpPost("google-login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        try
        {
            var result = await _authService.GoogleLoginAsync(request);
            return Ok(ApiResponse<AuthResponse>.SuccessResponse(result, "Google login successful."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<AuthResponse>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Change password of current user. Requires authentication.
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<ActionResult<ApiResponse<bool>>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(ApiResponse<bool>.FailResponse("User not authenticated."));
            }

            var result = await _authService.ChangePasswordAsync(userId, request);
            return Ok(ApiResponse<bool>.SuccessResponse(result, "Password changed successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<bool>.FailResponse(ex.Message));
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

    /// <summary>
    /// Initiate forgot password process. Sends reset email.
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<ActionResult<ApiResponse<bool>>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        try
        {
            var clientUrl = _configuration["ClientSettings:ClientUrl"] ?? "http://localhost:5173";
            var result = await _authService.ForgotPasswordAsync(request, clientUrl);
            return Ok(ApiResponse<bool>.SuccessResponse(result, "Reset password email sent successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<bool>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Reset password using token.
    /// </summary>
    [HttpPost("reset-password")]
    public async Task<ActionResult<ApiResponse<bool>>> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            var result = await _authService.ResetPasswordAsync(request);
            return Ok(ApiResponse<bool>.SuccessResponse(result, "Password has been reset successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<bool>.FailResponse(ex.Message));
        }
    }
}
