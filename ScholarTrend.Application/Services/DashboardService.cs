using ScholarTrend.Application.DTOs.Bookmarks;
using ScholarTrend.Application.DTOs.Dashboard;
using ScholarTrend.Application.DTOs.Follows;
using ScholarTrend.Application.DTOs.Notifications;
using ScholarTrend.Application.DTOs.Trends;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;

namespace ScholarTrend.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITrendService _trendService;
    private readonly IStatisticsRepository _statistics;

    public DashboardService(
        IUnitOfWork unitOfWork,
        ITrendService trendService,
        IStatisticsRepository statistics)
    {
        _unitOfWork = unitOfWork;
        _trendService = trendService;
        _statistics = statistics;
    }

    public async Task<PersonalDashboardDto> GetPersonalDashboardAsync(string userId)
    {
        var bookmarks = await _unitOfWork.Bookmarks.GetUserBookmarksAsync(userId);
        var followedTopics = await _unitOfWork.Follows.GetUserFollowedTopicsAsync(userId);
        var followedJournals = await _unitOfWork.Follows.GetUserFollowedJournalsAsync(userId);
        var notifications = await _unitOfWork.Notifications.GetUserNotificationsAsync(userId, null, 5);
        var unreadCount = await _unitOfWork.Notifications.GetUnreadCountAsync(userId);
        var topTopics = await _trendService.GetTopTopicsAsync(new DTOs.Trends.TrendFilterRequest { Top = 5 });

        return new PersonalDashboardDto
        {
            BookmarkCount = bookmarks.Count(),
            FollowedTopicsCount = followedTopics.Count,
            FollowedJournalsCount = followedJournals.Count,
            UnreadNotifications = unreadCount,
            RecentBookmarks = bookmarks.Take(5).Select(b => new BookmarkDto
            {
                Id = b.Id,
                PaperId = b.PaperId,
                Title = b.Paper.Title,
                PublicationYear = b.Paper.PublicationYear,
                CitationCount = b.Paper.CitationCount,
                JournalName = b.Paper.Journal?.Name,
                SavedAt = b.SavedAt
            }).ToList(),
            FollowedTopics = followedTopics.Select(f => new FollowItemDto
            {
                Id = f.Id,
                TargetId = f.TopicId,
                Name = f.Topic.TopicName,
                Type = "topic",
                FollowedAt = f.FollowedAt
            }).ToList(),
            FollowedJournals = followedJournals.Select(f => new FollowItemDto
            {
                Id = f.Id,
                TargetId = f.JournalId,
                Name = f.Journal.Name,
                Type = "journal",
                FollowedAt = f.FollowedAt
            }).ToList(),
            RecentNotifications = notifications.Select(n => new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                TargetUrl = n.TargetUrl,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                ReadAt = n.ReadAt
            }).ToList(),
            RecommendedTopics = topTopics.ToList()
        };
    }

    public async Task<OverviewDashboardDto> GetOverviewAsync()
    {
        var trendFilter = new TrendFilterRequest { Top = 5 };

        return new OverviewDashboardDto
        {
            TotalPapers = await _statistics.CountPapersAsync(),
            TotalKeywords = await _statistics.CountKeywordsAsync(),
            TotalTopics = await _statistics.CountTopicsAsync(),
            TotalJournals = await _statistics.CountJournalsAsync(),
            TotalAuthors = await _statistics.CountAuthorsAsync(),
            PublicationTrend = (await _trendService.GetPublicationTrendAsync(trendFilter)).ToList(),
            TopKeywords = (await _trendService.GetTopKeywordsAsync(trendFilter)).ToList(),
            TopTopics = (await _trendService.GetTopTopicsAsync(trendFilter)).ToList()
        };
    }
}
