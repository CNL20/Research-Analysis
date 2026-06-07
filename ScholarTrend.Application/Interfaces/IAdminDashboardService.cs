using ScholarTrend.Application.DTOs.Dashboard;

namespace ScholarTrend.Application.Interfaces;

public interface IAdminDashboardService
{
    Task<AdminDashboardDto> GetAdminDashboardAsync();
}
