using ScholarTrend.Application.DTOs.Notifications;

namespace ScholarTrend.Application.Interfaces;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(string userId, bool? isRead, int limit = 20);
    Task<int> GetUnreadCountAsync(string userId);
    Task MarkAsReadAsync(string userId, int notificationId);
    Task MarkAllAsReadAsync(string userId);
    Task<NotificationSettingDto> GetSettingsAsync(string userId);
    Task<NotificationSettingDto> UpdateSettingsAsync(string userId, NotificationSettingDto request);
    Task NotifyFollowersForNewPaperAsync(int paperId);
}
