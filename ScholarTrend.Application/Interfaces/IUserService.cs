using ScholarTrend.Application.DTOs.Auth;

namespace ScholarTrend.Application.Interfaces;

public interface IUserService
{
    Task<IReadOnlyList<UserListItemDto>> GetUsersAsync(UserFilterRequest? filter = null);
    Task<UserListItemDto> GetUserByIdAsync(string userId);
    Task<UserListItemDto> UpdateUserStatusAsync(string userId, UpdateUserStatusRequest request, string adminUserId);
    Task<UserListItemDto> UpdateUserRoleAsync(string userId, UpdateUserRoleRequest request, string adminUserId);
}
