using ScholarTrend.Application.DTOs.Notifications;

namespace ScholarTrend.Application.Interfaces;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(string userId, bool? isRead, int limit = 20, string? type = null);
    Task<int> GetUnreadCountAsync(string userId, string? type = null);
    Task MarkAsReadAsync(string userId, int notificationId);
    Task MarkAllAsReadAsync(string userId);
    Task<NotificationSettingDto> GetSettingsAsync(string userId);
    Task<NotificationSettingDto> UpdateSettingsAsync(string userId, NotificationSettingDto request);
    Task NotifyFollowersForNewPaperAsync(int paperId);
    Task NotifyAdminsPendingSyncAsync(int proposalId, int pendingCount);
    Task NotifyAdminsPaperEnrichmentIssueAsync(
        int paperId,
        string paperTitle,
        IReadOnlyList<string> missingFields,
        IReadOnlyList<string> fetchErrors);
    Task NotifyAdminsPaperEnrichmentCompleteAsync(int paperId, string paperTitle, int sourceCount);
}
