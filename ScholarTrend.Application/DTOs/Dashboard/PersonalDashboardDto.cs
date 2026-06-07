using ScholarTrend.Application.DTOs.Bookmarks;
using ScholarTrend.Application.DTOs.Follows;
using ScholarTrend.Application.DTOs.Notifications;
using ScholarTrend.Application.DTOs.Trends;

namespace ScholarTrend.Application.DTOs.Dashboard;

public class PersonalDashboardDto
{
    public int BookmarkCount { get; set; }
    public int FollowedTopicsCount { get; set; }
    public int FollowedJournalsCount { get; set; }
    public int UnreadNotifications { get; set; }
    public List<BookmarkDto> RecentBookmarks { get; set; } = [];
    public List<FollowItemDto> FollowedTopics { get; set; } = [];
    public List<FollowItemDto> FollowedJournals { get; set; } = [];
    public List<NotificationDto> RecentNotifications { get; set; } = [];
    public List<TopTrendItemDto> RecommendedTopics { get; set; } = [];
}
