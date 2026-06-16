using ScholarTrend.Application.DTOs.Auth;

namespace ScholarTrend.Application.Interfaces;

/// <summary>
/// Service interface for authentication operations (register, login, profile).
/// </summary>
public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request);
    Task<UserProfileDto> GetProfileAsync(string userId);
    Task<UserProfileDto> UpdateProfileAsync(string userId, UpdateProfileRequest request);
    Task<bool> ChangePasswordAsync(string userId, ChangePasswordRequest request);
    Task<bool> VerifyEmailAsync(VerifyEmailRequest request);
    Task<bool> ResendVerificationEmailAsync(ResendVerifyEmailRequest request, string clientUrl);
    Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request, string clientUrl);
    Task<bool> ResetPasswordAsync(ResetPasswordRequest request);


}
