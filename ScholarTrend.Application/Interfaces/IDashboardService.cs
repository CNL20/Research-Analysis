using ScholarTrend.Application.DTOs.Dashboard;

namespace ScholarTrend.Application.Interfaces;

public interface IDashboardService
{
    Task<PersonalDashboardDto> GetPersonalDashboardAsync(string userId);
    Task<OverviewDashboardDto> GetOverviewAsync();
}
