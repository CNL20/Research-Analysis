using Microsoft.AspNetCore.Identity;
using ScholarTrend.Application.DTOs.Notifications;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Domain.Constants;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<User> _userManager;

    public NotificationService(IUnitOfWork unitOfWork, UserManager<User> userManager)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(string userId, bool? isRead, int limit = 20, string? type = null)
    {
        var notifications = await _unitOfWork.Notifications.GetUserNotificationsAsync(userId, isRead, limit, type);
        return notifications.Select(MapToDto).ToList();
    }

    public Task<int> GetUnreadCountAsync(string userId, string? type = null)
    {
        return _unitOfWork.Notifications.GetUnreadCountAsync(userId, type);
    }

    public async Task MarkAsReadAsync(string userId, int notificationId)
    {
        var notification = await _unitOfWork.Notifications.GetByIdForUserAsync(notificationId, userId);
        if (notification == null)
        {
            throw new InvalidOperationException("Notification not found.");
        }

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();
    }

    public Task MarkAllAsReadAsync(string userId)
    {
        return _unitOfWork.Notifications.MarkAllAsReadAsync(userId);
    }

    public async Task<NotificationSettingDto> GetSettingsAsync(string userId)
    {
        var settings = await GetOrCreateSettingsAsync(userId);
        return MapSettingsToDto(settings);
    }

    public async Task<NotificationSettingDto> UpdateSettingsAsync(string userId, NotificationSettingDto request)
    {
        var settings = await GetOrCreateSettingsAsync(userId);
        settings.EmailEnabled = request.EmailEnabled;
        settings.TopicAlertEnabled = request.TopicAlertEnabled;
        settings.Frequency = request.Frequency;

        await _unitOfWork.SaveChangesAsync();
        return MapSettingsToDto(settings);
    }

    public async Task NotifyFollowersForNewPaperAsync(int paperId)
    {
        var paper = await _unitOfWork.ResearchPapers.GetPaperWithDetailsAsync(paperId);
        if (paper == null)
        {
            return;
        }

        var notifiedUsers = new HashSet<string>();

        foreach (var topicId in paper.PaperTopics.Select(pt => pt.TopicId))
        {
            var followers = await _unitOfWork.Follows.GetTopicFollowerUserIdsAsync(topicId);
            foreach (var userId in followers)
            {
                await TryNotifyUserAsync(userId, notifiedUsers, "New paper in followed topic",
                    $"A new paper \"{paper.Title}\" was published in a topic you follow.",
                    $"/papers/{paper.Id}");
            }
        }

        if (paper.JournalId.HasValue)
        {
            var followers = await _unitOfWork.Follows.GetJournalFollowerUserIdsAsync(paper.JournalId.Value);
            foreach (var userId in followers)
            {
                await TryNotifyUserAsync(userId, notifiedUsers, "New paper in followed journal",
                    $"A new paper \"{paper.Title}\" was published in a journal you follow.",
                    $"/papers/{paper.Id}");
            }
        }

        foreach (var authorId in paper.PaperAuthors.Select(pa => pa.AuthorId))
        {
            var followers = await _unitOfWork.Follows.GetAuthorFollowerUserIdsAsync(authorId);
            foreach (var userId in followers)
            {
                await TryNotifyUserAsync(userId, notifiedUsers, "New paper by followed author",
                    $"A new paper \"{paper.Title}\" was published by an author you follow.",
                    $"/papers/{paper.Id}");
            }
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task NotifyAdminsPendingSyncAsync(int proposalId, int pendingCount)
    {
        var admins = await _userManager.GetUsersInRoleAsync(RoleConstants.Admin);

        foreach (var admin in admins)
        {
            await _unitOfWork.Notifications.AddAsync(new Notification
            {
                UserId = admin.Id,
                Title = "Papers pending sync approval",
                Message = $"{pendingCount} new paper(s) are waiting for your approval before they are synced.",
                TargetUrl = $"/admin/sync/pending/{proposalId}",
                Type = "Admin",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task NotifyAdminsPaperEnrichmentIssueAsync(
        int paperId,
        string paperTitle,
        IReadOnlyList<string> missingFields,
        IReadOnlyList<string> fetchErrors)
    {
        if (missingFields.Count == 0 && fetchErrors.Count == 0)
        {
            return;
        }

        var details = new List<string>();
        if (missingFields.Count > 0)
        {
            details.Add($"Missing: {string.Join(", ", missingFields)}");
        }

        if (fetchErrors.Count > 0)
        {
            details.Add($"Fetch errors: {string.Join("; ", fetchErrors)}");
        }

        var message =
            $"Paper \"{paperTitle}\" (#{paperId}) enrichment incomplete. {string.Join(". ", details)}.";

        var admins = await _userManager.GetUsersInRoleAsync(RoleConstants.Admin);
        foreach (var admin in admins)
        {
            await _unitOfWork.Notifications.AddAsync(new Notification
            {
                UserId = admin.Id,
                Title = "Paper enrichment incomplete",
                Message = message,
                TargetUrl = $"/papers/{paperId}",
                Type = "Admin",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task NotifyAdminsPaperEnrichmentCompleteAsync(
        int paperId,
        string paperTitle,
        int sourceCount)
    {
        var message =
            $"Paper \"{paperTitle}\" (#{paperId}) enrichment completed with full metadata ({sourceCount} source(s)).";

        var admins = await _userManager.GetUsersInRoleAsync(RoleConstants.Admin);
        foreach (var admin in admins)
        {
            await _unitOfWork.Notifications.AddAsync(new Notification
            {
                UserId = admin.Id,
                Title = "Paper enrichment complete",
                Message = message,
                TargetUrl = $"/papers/{paperId}",
                Type = "Admin",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private async Task TryNotifyUserAsync(
        string userId,
        HashSet<string> notifiedUsers,
        string title,
        string message,
        string targetUrl)
    {
        if (!notifiedUsers.Add(userId))
        {
            return;
        }

        if (!await IsAlertEnabledAsync(userId))
        {
            return;
        }

        await _unitOfWork.Notifications.AddAsync(new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            TargetUrl = targetUrl,
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task<bool> IsAlertEnabledAsync(string userId)
    {
        var settings = await _unitOfWork.Notifications.GetSettingsAsync(userId);
        return settings?.TopicAlertEnabled ?? true;
    }

    private async Task<NotificationSetting> GetOrCreateSettingsAsync(string userId)
    {
        var settings = await _unitOfWork.Notifications.GetSettingsAsync(userId);
        if (settings != null)
        {
            return settings;
        }

        settings = new NotificationSetting
        {
            UserId = userId,
            EmailEnabled = true,
            TopicAlertEnabled = true,
            Frequency = "Daily"
        };

        await _unitOfWork.Notifications.AddSettingsAsync(settings);
        await _unitOfWork.SaveChangesAsync();
        return settings;
    }

    private static NotificationDto MapToDto(Notification notification)
    {
        return new NotificationDto
        {
            Id = notification.Id,
            Title = notification.Title,
            Message = notification.Message,
            TargetUrl = notification.TargetUrl,
            IsRead = notification.IsRead,
            Type = notification.Type,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt
        };
    }

    private static NotificationSettingDto MapSettingsToDto(NotificationSetting settings)
    {
        return new NotificationSettingDto
        {
            EmailEnabled = settings.EmailEnabled,
            TopicAlertEnabled = settings.TopicAlertEnabled,
            Frequency = settings.Frequency
        };
    }
}
