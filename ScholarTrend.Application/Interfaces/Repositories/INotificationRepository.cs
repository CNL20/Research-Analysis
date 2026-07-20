using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface INotificationRepository
{
    Task AddAsync(Notification notification);
    Task<IReadOnlyList<Notification>> GetUserNotificationsAsync(string userId, bool? isRead, int limit, string? type = null);
    Task<int> GetUnreadCountAsync(string userId, string? type = null);
    Task<Notification?> GetByIdForUserAsync(int id, string userId);
    Task MarkAllAsReadAsync(string userId);
    Task<NotificationSetting?> GetSettingsAsync(string userId);
    Task AddSettingsAsync(NotificationSetting settings);
}
