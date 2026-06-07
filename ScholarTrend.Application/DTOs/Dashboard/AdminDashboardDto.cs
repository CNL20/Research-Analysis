using ScholarTrend.Application.DTOs.Sync;
using ScholarTrend.Application.DTOs.Trends;

namespace ScholarTrend.Application.DTOs.Dashboard;

public class AdminDashboardDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalPapers { get; set; }
    public int TotalKeywords { get; set; }
    public int TotalTopics { get; set; }
    public int TotalJournals { get; set; }
    public int TotalBookmarks { get; set; }
    public int TotalFollows { get; set; }
    public Dictionary<string, int> UsersByRole { get; set; } = [];
    public SyncLogDto? LastSync { get; set; }
    public List<SyncLogDto> RecentSyncLogs { get; set; } = [];
    public List<ApiDataSourceDto> DataSources { get; set; } = [];
    public List<TrendDataPointDto> PublicationTrend { get; set; } = [];
    public List<TopTrendItemDto> TopKeywords { get; set; } = [];
}
