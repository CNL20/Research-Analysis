using ScholarTrend.Application.DTOs.Auth;
using ScholarTrend.Application.DTOs.Common;

namespace ScholarTrend.Application.Interfaces;

public interface IUserService
{
    Task<PagedResult<UserListItemDto>> GetUsersAsync(UserFilterRequest? filter = null);
    Task<UserListItemDto> GetUserByIdAsync(string userId);
    Task<UserListItemDto> UpdateUserStatusAsync(string userId, UpdateUserStatusRequest request, string adminUserId);
    Task<UserListItemDto> UpdateUserRoleAsync(string userId, UpdateUserRoleRequest request, string adminUserId);
}
