using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.DTOs.Notifications;
using ScholarTrend.Application.Interfaces;

namespace ScholarTrend.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NotificationDto>>>> GetNotifications(
        [FromQuery] bool? isRead,
        [FromQuery] int limit = 20)
    {
        var result = await _notificationService.GetNotificationsAsync(GetUserId(), isRead, limit);
        return Ok(ApiResponse<IReadOnlyList<NotificationDto>>.SuccessResponse(result));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<ApiResponse<object>>> GetUnreadCount()
    {
        var count = await _notificationService.GetUnreadCountAsync(GetUserId());
        return Ok(ApiResponse<object>.SuccessResponse(new { count }));
    }

    [HttpPatch("{id:int}/read")]
    public async Task<ActionResult<ApiResponse<object>>> MarkAsRead(int id)
    {
        try
        {
            await _notificationService.MarkAsReadAsync(GetUserId(), id);
            return Ok(ApiResponse<object>.SuccessResponse(new { }, "Notification marked as read."));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<object>.FailResponse(ex.Message));
        }
    }

    [HttpPatch("read-all")]
    public async Task<ActionResult<ApiResponse<object>>> MarkAllAsRead()
    {
        await _notificationService.MarkAllAsReadAsync(GetUserId());
        return Ok(ApiResponse<object>.SuccessResponse(new { }, "All notifications marked as read."));
    }

    [HttpGet("settings")]
    public async Task<ActionResult<ApiResponse<NotificationSettingDto>>> GetSettings()
    {
        var result = await _notificationService.GetSettingsAsync(GetUserId());
        return Ok(ApiResponse<NotificationSettingDto>.SuccessResponse(result));
    }

    [HttpPut("settings")]
    public async Task<ActionResult<ApiResponse<NotificationSettingDto>>> UpdateSettings([FromBody] NotificationSettingDto request)
    {
        var result = await _notificationService.UpdateSettingsAsync(GetUserId(), request);
        return Ok(ApiResponse<NotificationSettingDto>.SuccessResponse(result, "Notification settings updated."));
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User not authenticated.");
    }
}
