using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ScholarTrend.Application.DTOs.Auth;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Domain.Enums;

namespace ScholarTrend.Application.Services;

/// <summary>
/// Authentication service handling register, login, refresh token, and profile operations.
/// </summary>
public class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;

    public AuthService(
        UserManager<User> userManager,
        RoleManager<IdentityRole> roleManager,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        IEmailService emailService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _emailService = emailService;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            throw new InvalidOperationException("Email is already registered.");
        }

        var user = new User
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            Institution = request.Institution,
            ResearchField = request.ResearchField,
            EmailConfirmed = false, // Chờ verify
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create user: {errors}");
        }

        await EnsureDefaultRoleExistsAsync();
        await _userManager.AddToRoleAsync(user, UserRole.LecturerStudent.ToString());

        // Sinh Token xác thực
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
    
        // Đọc link Frontend từ appsettings.json
        var clientUrl = _configuration["ClientSettings:ClientUrl"] ?? "http://localhost:5173";
    
        // Link trỏ đến trang verify của Frontend
        var verificationLink = $"{clientUrl}/verify-email?email={System.Web.HttpUtility.UrlEncode(user.Email)}&token={System.Web.HttpUtility.UrlEncode(token)}";

        // Gửi email thực tế thông qua dịch vụ đã tạo ở Phần 2
        var emailBody = $"<h3>Chào mừng {user.FullName} đến với ScholarTrend!</h3>" +
                        $"<p>Vui lòng click vào link bên dưới để xác thực tài khoản của bạn:</p>" +
                        $"<a href='{verificationLink}' style='padding: 10px 20px; background-color: #4CAF50; color: white; text-decoration: none; border-radius: 5px;'>Xác thực ngay</a>";

        await _emailService.SendEmailAsync(user.Email, "Xác thực tài khoản ScholarTrend", emailBody);

        return await BuildAuthResponseAsync(user);
    }
    public async Task<bool> VerifyEmailAsync(VerifyEmailRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }
        var result = await _userManager.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Verification failed: {errors}");
        }
        return true;
    }
    public async Task<bool> ResendVerificationEmailAsync(ResendVerifyEmailRequest request, string clientUrl)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }
        if (user.EmailConfirmed)
        {
            throw new InvalidOperationException("Email is already confirmed.");
        }
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var verificationLink = $"{clientUrl}/verify-email?email={System.Web.HttpUtility.UrlEncode(user.Email)}&token={System.Web.HttpUtility.UrlEncode(token)}";
        var emailBody = $"<h3>Yêu cầu gửi lại link xác thực ScholarTrend</h3>" +
                        $"<p>Vui lòng click vào link bên dưới để hoàn tất xác thực:</p>" +
                        $"<a href='{verificationLink}' style='padding: 10px 20px; background-color: #4CAF50; color: white; text-decoration: none; border-radius: 5px;'>Xác thực ngay</a>";
        await _emailService.SendEmailAsync(user.Email, "Xác thực tài khoản ScholarTrend (Gửi lại)", emailBody);
        return true;
    }


    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            throw new InvalidOperationException("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            throw new InvalidOperationException("Account has been deactivated. Please contact administrator.");
        }

        if (!user.EmailConfirmed)
        {
            throw new InvalidOperationException("Please confirm your email before logging in.");
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isPasswordValid)
        {
            throw new InvalidOperationException("Invalid email or password.");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        return await BuildAuthResponseAsync(user);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var storedToken = await _refreshTokenRepository.GetActiveByTokenAsync(request.RefreshToken);
        if (storedToken == null)
        {
            throw new InvalidOperationException("Invalid or expired refresh token.");
        }

        await _refreshTokenRepository.RevokeAsync(storedToken);
        await _unitOfWork.SaveChangesAsync();

        return await BuildAuthResponseAsync(storedToken.User);
    }

    public async Task<UserProfileDto> GetProfileAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        var roles = await _userManager.GetRolesAsync(user);

        return MapToProfile(user, roles);
    }

    public async Task<UserProfileDto> UpdateProfileAsync(string userId, UpdateProfileRequest request)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        user.FullName = request.FullName;
        user.Institution = request.Institution;
        user.ResearchField = request.ResearchField;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to update profile: {errors}");
        }

        var roles = await _userManager.GetRolesAsync(user);
        return MapToProfile(user, roles);
    }

    private static UserProfileDto MapToProfile(User user, IList<string> roles)
    {
        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email!,
            FullName = user.FullName,
            Institution = user.Institution,
            ResearchField = user.ResearchField,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            Roles = roles.ToList()
        };
    }

    private async Task EnsureDefaultRoleExistsAsync()
    {
        var roleName = UserRole.LecturerStudent.ToString();
        if (await _roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var result = await _roleManager.CreateAsync(new IdentityRole(roleName));
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to ensure default role exists: {errors}");
        }
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(User user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = GenerateJwtToken(user, roles);
        var refreshToken = await CreateRefreshTokenAsync(user);

        return new AuthResponse
        {
            Token = accessToken,
            Expiration = DateTime.UtcNow.AddMinutes(GetTokenExpirationMinutes()),
            RefreshToken = refreshToken.Token,
            RefreshTokenExpiration = refreshToken.ExpiresAt,
            UserId = user.Id,
            Email = user.Email!,
            FullName = user.FullName,
            Roles = roles.ToList()
        };
    }

    private async Task<RefreshToken> CreateRefreshTokenAsync(User user)
    {
        var refreshToken = new RefreshToken
        {
            Token = GenerateRefreshTokenValue(),
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(GetRefreshTokenExpirationDays()),
            CreatedAt = DateTime.UtcNow
        };

        await _refreshTokenRepository.AddAsync(refreshToken);
        await _unitOfWork.SaveChangesAsync();

        return refreshToken;
    }

    private string GenerateJwtToken(User user, IList<string> roles)
    {
        var secretKey = GetJwtSecretKey();
        var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secretKey));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            expires: DateTime.UtcNow.AddMinutes(GetTokenExpirationMinutes()),
            claims: claims,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshTokenValue()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }

    private string GetJwtSecretKey()
    {
        var secretKey = _configuration["Authentication:Jwt:SecretKey"]
            ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY");

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("JWT SecretKey is missing from configuration.");
        }

        return secretKey;
    }

    private int GetTokenExpirationMinutes()
    {
        var minutes = _configuration.GetSection("Authentication:Jwt")["ExpirationMinutes"];
        return int.TryParse(minutes, out var result) ? result : 60;
    }

    private int GetRefreshTokenExpirationDays()
    {
        var days = _configuration.GetSection("Authentication:Jwt")["RefreshTokenExpirationDays"];
        return int.TryParse(days, out var result) ? result : 7;
    }

    public async Task<bool> ChangePasswordAsync(string userId, ChangePasswordRequest request)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }
        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to change password: {errors}");
        }
        return true;
    }

    public async Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request, string clientUrl)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            // Trả về true vì lý do bảo mật (tránh lộ email tồn tại trong hệ thống)
            return true;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetLink = $"{clientUrl}/reset-password?email={System.Web.HttpUtility.UrlEncode(user.Email)}&token={System.Web.HttpUtility.UrlEncode(token)}";

        var emailBody = $"<h3>Yêu cầu đặt lại mật khẩu ScholarTrend</h3>" +
                        $"<p>Vui lòng click vào link bên dưới để tiến hành đặt lại mật khẩu của bạn:</p>" +
                        $"<a href='{resetLink}' style='padding: 10px 20px; background-color: #f44336; color: white; text-decoration: none; border-radius: 5px;'>Đặt lại mật khẩu</a>";

        await _emailService.SendEmailAsync(user.Email!, "Đặt lại mật khẩu ScholarTrend", emailBody);
        return true;
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        // Tự động giải mã token nếu token chứa ký tự đặc biệt dạng %XX
        var decodedToken = request.Token;
        if (request.Token.Contains("%"))
        {
            decodedToken = System.Web.HttpUtility.UrlDecode(request.Token);
        }

        var result = await _userManager.ResetPasswordAsync(user, decodedToken, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Reset password failed: {errors}");
        }

        return true;
    }
}
