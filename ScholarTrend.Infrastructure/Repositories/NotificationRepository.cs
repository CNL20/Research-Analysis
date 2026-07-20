using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly ScholarTrendDbContext _context;

    public NotificationRepository(ScholarTrendDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Notification notification)
    {
        await _context.Notifications.AddAsync(notification);
    }

    public async Task<IReadOnlyList<Notification>> GetUserNotificationsAsync(string userId, bool? isRead, int limit, string? type = null)
    {
        var query = _context.Notifications
            .Where(n => n.UserId == userId);

        if (isRead.HasValue)
        {
            query = query.Where(n => n.IsRead == isRead.Value);
        }

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(n => n.Type == type);
        }

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public Task<int> GetUnreadCountAsync(string userId, string? type = null)
    {
        var query = _context.Notifications.Where(n => n.UserId == userId && !n.IsRead);
        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(n => n.Type == type);
        }
        return query.CountAsync();
    }

    public Task<Notification?> GetByIdForUserAsync(int id, string userId)
    {
        return _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, DateTime.UtcNow));
    }

    public Task<NotificationSetting?> GetSettingsAsync(string userId)
    {
        return _context.NotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == userId);
    }

    public async Task AddSettingsAsync(NotificationSetting settings)
    {
        await _context.NotificationSettings.AddAsync(settings);
    }
}
