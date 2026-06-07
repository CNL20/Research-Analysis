using Microsoft.AspNetCore.Identity;
using ScholarTrend.Application.DTOs.Dashboard;
using ScholarTrend.Application.DTOs.Sync;
using ScholarTrend.Application.DTOs.Trends;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Constants;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Services;

public class AdminDashboardService : IAdminDashboardService
{
    private readonly IStatisticsRepository _statistics;
    private readonly ISyncService _syncService;
    private readonly ITrendService _trendService;
    private readonly UserManager<User> _userManager;

    public AdminDashboardService(
        IStatisticsRepository statistics,
        ISyncService syncService,
        ITrendService trendService,
        UserManager<User> userManager)
    {
        _statistics = statistics;
        _syncService = syncService;
        _trendService = trendService;
        _userManager = userManager;
    }

    public async Task<AdminDashboardDto> GetAdminDashboardAsync()
    {
        var trendFilter = new TrendFilterRequest { Top = 5 };
        var syncLogs = await _syncService.GetSyncLogsAsync(5);
        var dataSources = await _syncService.GetDataSourcesAsync();
        var publicationTrend = await _trendService.GetPublicationTrendAsync(trendFilter);
        var topKeywords = await _trendService.GetTopKeywordsAsync(trendFilter);

        return new AdminDashboardDto
        {
            TotalUsers = _userManager.Users.Count(),
            ActiveUsers = await _statistics.CountActiveUsersAsync(),
            TotalPapers = await _statistics.CountPapersAsync(),
            TotalKeywords = await _statistics.CountKeywordsAsync(),
            TotalTopics = await _statistics.CountTopicsAsync(),
            TotalJournals = await _statistics.CountJournalsAsync(),
            TotalBookmarks = await _statistics.CountBookmarksAsync(),
            TotalFollows = await _statistics.CountFollowsAsync(),
            UsersByRole = await GetUsersByRoleAsync(),
            LastSync = syncLogs.FirstOrDefault(),
            RecentSyncLogs = syncLogs.ToList(),
            DataSources = dataSources.ToList(),
            PublicationTrend = publicationTrend.ToList(),
            TopKeywords = topKeywords.ToList()
        };
    }

    private async Task<Dictionary<string, int>> GetUsersByRoleAsync()
    {
        var result = new Dictionary<string, int>();
        foreach (var role in RoleConstants.All)
        {
            var users = await _userManager.GetUsersInRoleAsync(role);
            result[role] = users.Count;
        }

        return result;
    }
}
