using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Moq;
using ScholarTrend.Application.DTOs.Auth;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Application.Services;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Domain.Enums;

namespace ScholarTrend.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<UserManager<User>> _mockUserManager;
    private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
    private readonly Mock<IRefreshTokenRepository> _mockRefreshTokenRepo;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        // UserManager requires a lot of setup to mock
        _mockUserManager = MockUserManager<User>();
        _mockRoleManager = MockRoleManager<IdentityRole>();
        _mockRefreshTokenRepo = new Mock<IRefreshTokenRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockConfig = new Mock<IConfiguration>();

        // Setup common config
        _mockConfig.Setup(c => c["Authentication:Jwt:SecretKey"]).Returns("SuperSecretKeyAtLeast32CharactersLong!");
        _mockConfig.Setup(c => c["Authentication:Jwt:ExpirationMinutes"]).Returns("1440");
        _mockConfig.Setup(c => c["Authentication:Jwt:RefreshTokenExpirationDays"]).Returns("30");
        
        _authService = new AuthService(
            _mockUserManager.Object,
            _mockRoleManager.Object,
            _mockRefreshTokenRepo.Object,
            _mockUnitOfWork.Object,
            _mockConfig.Object
        );
    }

    [Fact]
    public async Task LoginAsync_ShouldThrowException_WhenUserNotFound()
    {
        // Arrange
        _mockUserManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((User)null!);

        var request = new LoginRequest { Email = "test@example.com", Password = "Password123!" };

        // Act & Assert
        await _authService.Invoking(s => s.LoginAsync(request))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Invalid email or password.");
    }

    [Fact]
    public async Task LoginAsync_ShouldThrowException_WhenUserIsDeactivated()
    {
        // Arrange
        var user = new User { Email = "test@example.com", IsActive = false };
        _mockUserManager.Setup(m => m.FindByEmailAsync(user.Email))
            .ReturnsAsync(user);

        var request = new LoginRequest { Email = user.Email, Password = "Password123!" };

        // Act & Assert
        await _authService.Invoking(s => s.LoginAsync(request))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Account has been deactivated.*");
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnAuthResponse_WhenCredentialsAreValid()
    {
        // Arrange
        var user = new User { Id = "1", Email = "test@example.com", FullName = "Test User", IsActive = true };
        _mockUserManager.Setup(m => m.FindByEmailAsync(user.Email))
            .ReturnsAsync(user);
        _mockUserManager.Setup(m => m.CheckPasswordAsync(user, "Password123!"))
            .ReturnsAsync(true);
        _mockUserManager.Setup(m => m.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { UserRole.LecturerStudent.ToString() });

        _mockRefreshTokenRepo.Setup(r => r.AddAsync(It.IsAny<RefreshToken>()))
            .Returns(Task.CompletedTask);

        var request = new LoginRequest { Email = user.Email, Password = "Password123!" };

        // Act
        var response = await _authService.LoginAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Email.Should().Be(user.Email);
        response.Token.Should().NotBeEmpty();
        _mockRefreshTokenRepo.Verify(r => r.AddAsync(It.IsAny<RefreshToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_ShouldThrowException_WhenEmailExists()
    {
        // Arrange
        var user = new User { Email = "test@example.com" };
        _mockUserManager.Setup(m => m.FindByEmailAsync(user.Email))
            .ReturnsAsync(user);

        var request = new RegisterRequest { Email = user.Email, FullName = "Test", Password = "Pass" };

        // Act & Assert
        await _authService.Invoking(s => s.RegisterAsync(request))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Email is already registered.");
    }

    // Helper methods for mocking Identity
    private static Mock<UserManager<TUser>> MockUserManager<TUser>() where TUser : class
    {
        var store = new Mock<IUserStore<TUser>>();
        return new Mock<UserManager<TUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static Mock<RoleManager<TRole>> MockRoleManager<TRole>() where TRole : class
    {
        var store = new Mock<IRoleStore<TRole>>();
        return new Mock<RoleManager<TRole>>(store.Object, null!, null!, null!, null!);
    }
}
