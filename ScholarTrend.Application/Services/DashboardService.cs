using ScholarTrend.Application.DTOs.Bookmarks;
using ScholarTrend.Application.DTOs.Dashboard;
using ScholarTrend.Application.DTOs.Follows;
using ScholarTrend.Application.DTOs.Notifications;
using ScholarTrend.Application.DTOs.Trends;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;

using Microsoft.Extensions.Caching.Memory;

namespace ScholarTrend.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITrendService _trendService;
    private readonly IStatisticsRepository _statistics;
    private readonly IMemoryCache _cache;

    public DashboardService(
        IUnitOfWork unitOfWork,
        ITrendService trendService,
        IStatisticsRepository statistics,
        IMemoryCache cache)
    {
        _unitOfWork = unitOfWork;
        _trendService = trendService;
        _statistics = statistics;
        _cache = cache;
    }

    public async Task<PersonalDashboardDto> GetPersonalDashboardAsync(string userId)
    {
        var (bookmarks, bookmarksCount) = await _unitOfWork.Bookmarks.GetUserBookmarksAsync(userId, 1, 5);
        var (followedTopics, followedTopicsCount) = await _unitOfWork.Follows.GetUserFollowedTopicsAsync(userId, 1, 5);
        var (followedJournals, followedJournalsCount) = await _unitOfWork.Follows.GetUserFollowedJournalsAsync(userId, 1, 5);
        var notifications = await _unitOfWork.Notifications.GetUserNotificationsAsync(userId, null, 5, "User");
        var unreadCount = await _unitOfWork.Notifications.GetUnreadCountAsync(userId, "User");
        var trendDashboard = await _trendService.GetDashboardAsync(new TrendFilterRequest { Top = 5 });
        var topTopics = trendDashboard.TopTopics;

        return new PersonalDashboardDto
        {
            BookmarkCount = bookmarksCount,
            FollowedTopicsCount = followedTopicsCount,
            FollowedJournalsCount = followedJournalsCount,
            UnreadNotifications = unreadCount,
            RecentBookmarks = bookmarks.Select(b => new BookmarkDto
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
            RecommendedTopics = topTopics
        };
    }

    public async Task<OverviewDashboardDto> GetOverviewAsync()
    {
        return await _cache.GetOrCreateAsync("dashboard:overview", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

            // Execute in ReadUncommitted transaction to prevent lock timeouts caused by background Hangfire jobs
            var transactionStarted = await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.ReadUncommitted);
            try 
            {
                var trendFilter = new TrendFilterRequest { Top = 5 };

                var totalPapers = await _statistics.CountPapersAsync();
                var totalKeywords = await _statistics.CountKeywordsAsync();
                var totalTopics = await _statistics.CountTopicsAsync();
                var totalJournals = await _statistics.CountJournalsAsync();
                var totalAuthors = await _statistics.CountAuthorsAsync();
                
                var trendDashboard = await _trendService.GetDashboardAsync(trendFilter);

                if (transactionStarted)
                {
                    await _unitOfWork.CommitTransactionAsync();
                }

                return new OverviewDashboardDto
                {
                    TotalPapers = totalPapers,
                    TotalKeywords = totalKeywords,
                    TotalTopics = totalTopics,
                    TotalJournals = totalJournals,
                    TotalAuthors = totalAuthors,
                    PublicationTrend = trendDashboard.PublicationTrend,
                    TopKeywords = trendDashboard.TopKeywords,
                    TopTopics = trendDashboard.TopTopics
                };
            }
            catch 
            {
                if (transactionStarted)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                }
                throw;
            }
        }) ?? new OverviewDashboardDto();
    }
}
