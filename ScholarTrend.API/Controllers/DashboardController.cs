using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.DTOs.Dashboard;
using ScholarTrend.Application.Interfaces;

namespace ScholarTrend.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("personal")]
    public async Task<ActionResult<ApiResponse<PersonalDashboardDto>>> GetPersonalDashboard()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User not authenticated.");

        var result = await _dashboardService.GetPersonalDashboardAsync(userId);
        return Ok(ApiResponse<PersonalDashboardDto>.SuccessResponse(result));
    }

    /// System overview dashboard with aggregate stats and top trends.
    /// </summary>
    [HttpGet("overview")]
    public async Task<ActionResult<ApiResponse<OverviewDashboardDto>>> GetOverview()
    {
        var result = await _dashboardService.GetOverviewAsync();
        return Ok(ApiResponse<OverviewDashboardDto>.SuccessResponse(result));
    }
}
