using Microsoft.AspNetCore.Identity;
using ScholarTrend.Application.DTOs.Auth;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Services;

public class UserService : IUserService
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public UserService(UserManager<User> userManager, RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IReadOnlyList<UserListItemDto>> GetUsersAsync(UserFilterRequest? filter = null)
    {
        var query = _userManager.Users.AsQueryable();

        if (filter?.IsActive is bool isActive)
        {
            query = query.Where(u => u.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(filter?.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(u =>
                u.FullName.Contains(search) ||
                (u.Email != null && u.Email.Contains(search)));
        }

        var users = query
            .OrderByDescending(u => u.CreatedAt)
            .ToList();

        var result = new List<UserListItemDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            if (!string.IsNullOrWhiteSpace(filter?.Role) &&
                !roles.Contains(filter.Role, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(MapToListItem(user, roles));
        }

        return result;
    }

    public async Task<UserListItemDto> GetUserByIdAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        return MapToListItem(user, roles);
    }

    public async Task<UserListItemDto> UpdateUserStatusAsync(
        string userId,
        UpdateUserStatusRequest request,
        string adminUserId)
    {
        if (userId == adminUserId && !request.IsActive)
        {
            throw new InvalidOperationException("You cannot deactivate your own account.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        user.IsActive = request.IsActive;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to update user status: {errors}");
        }

        var roles = await _userManager.GetRolesAsync(user);
        return MapToListItem(user, roles);
    }

    public async Task<UserListItemDto> UpdateUserRoleAsync(
        string userId,
        UpdateUserRoleRequest request,
        string adminUserId)
    {
        if (userId == adminUserId)
        {
            throw new InvalidOperationException("You cannot change your own role.");
        }

        if (!await _roleManager.RoleExistsAsync(request.Role))
        {
            throw new InvalidOperationException($"Role '{request.Role}' does not exist.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                var errors = string.Join(", ", removeResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to remove current roles: {errors}");
            }
        }

        var addResult = await _userManager.AddToRoleAsync(user, request.Role);
        if (!addResult.Succeeded)
        {
            var errors = string.Join(", ", addResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to assign new role: {errors}");
        }

        var roles = await _userManager.GetRolesAsync(user);
        return MapToListItem(user, roles);
    }

    private static UserListItemDto MapToListItem(User user, IList<string> roles)
    {
        return new UserListItemDto
        {
            Id = user.Id,
            Email = user.Email!,
            FullName = user.FullName,
            Institution = user.Institution,
            ResearchField = user.ResearchField,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            Roles = roles.ToList()
        };
    }
}
