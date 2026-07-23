using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.DTOs.Sync;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Domain.Constants;

namespace ScholarTrend.API.Controllers;

[Authorize(Roles = RoleConstants.Admin)]
[ApiController]
[Route("api/admin/sync")]
public class AdminSyncController : ControllerBase
{
    private readonly ISyncService _syncService;
    private readonly ISyncSchedulerService _syncSchedulerService;

    public AdminSyncController(ISyncService syncService, ISyncSchedulerService syncSchedulerService)
    {
        _syncService = syncService;
        _syncSchedulerService = syncSchedulerService;
    }

    [HttpGet("pending")]
    public async Task<ActionResult<ApiResponse<PagedResult<SyncProposalListItemDto>>>> GetPendingProposals(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _syncService.GetPendingProposalsAsync(page, pageSize);
        return Ok(ApiResponse<PagedResult<SyncProposalListItemDto>>.SuccessResponse(result));
    }

    [HttpGet("pending/{id:int}")]
    public async Task<ActionResult<ApiResponse<SyncProposalDto>>> GetPendingProposal(int id)
    {
        try
        {
            var result = await _syncService.GetPendingProposalByIdAsync(id);
            return Ok(ApiResponse<SyncProposalDto>.SuccessResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<SyncProposalDto>.FailResponse(ex.Message));
        }
    }

    [HttpPost("pending/{id:int}/approve")]
    public async Task<ActionResult<ApiResponse<ApproveSyncResultDto>>> ApprovePendingSync(
        int id,
        [FromBody] ApproveSyncRequest? request)
    {
        try
        {
            var adminUserId = GetUserId();
            var result = await _syncService.ApprovePendingSyncAsync(id, adminUserId, request ?? new ApproveSyncRequest());
            return Ok(ApiResponse<ApproveSyncResultDto>.SuccessResponse(result, result.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<ApproveSyncResultDto>.FailResponse(ex.Message));
        }
    }

    [HttpPost("approve-all-pending")]
    public async Task<ActionResult<ApiResponse<ApproveSyncResultDto>>> ApproveAllPendingSyncs()
    {
        try
        {
            var adminUserId = GetUserId();
            var totalApproved = await _syncService.ApproveAllPendingProposalsAsync(adminUserId);
            var result = new ApproveSyncResultDto
            {
                Status = "Success",
                PapersApproved = totalApproved,
                Message = $"Successfully approved {totalApproved} pending papers across all proposals."
            };
            return Ok(ApiResponse<ApproveSyncResultDto>.SuccessResponse(result, result.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<ApproveSyncResultDto>.FailResponse(ex.Message));
        }
    }

    [HttpPost("pending/{id:int}/reject")]
    public async Task<ActionResult<ApiResponse<ApproveSyncResultDto>>> RejectPendingSync(int id)
    {
        try
        {
            var adminUserId = GetUserId();
            var result = await _syncService.RejectPendingSyncAsync(id, adminUserId);
            return Ok(ApiResponse<ApproveSyncResultDto>.SuccessResponse(result, result.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<ApproveSyncResultDto>.FailResponse(ex.Message));
        }
    }

    [HttpGet("logs")]
    public async Task<ActionResult<ApiResponse<PagedResult<SyncLogDto>>>> GetLogs(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _syncService.GetSyncLogsAsync(page, pageSize);
        return Ok(ApiResponse<PagedResult<SyncLogDto>>.SuccessResponse(result));
    }

    [HttpGet("data-sources")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ApiDataSourceDto>>>> GetDataSources()
    {
        var result = await _syncService.GetDataSourcesAsync();
        return Ok(ApiResponse<IReadOnlyList<ApiDataSourceDto>>.SuccessResponse(result));
    }

    [HttpPatch("data-sources/{id:int}")]
    public async Task<ActionResult<ApiResponse<ApiDataSourceDto>>> UpdateDataSource(
        int id,
        [FromBody] UpdateApiDataSourceRequest request)
    {
        try
        {
            var result = await _syncService.UpdateDataSourceAsync(id, request);
            return Ok(ApiResponse<ApiDataSourceDto>.SuccessResponse(result, "Data source updated."));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<ApiDataSourceDto>.FailResponse(ex.Message));
        }
    }

    #region Schedule & Status

    /// <summary>
    /// Get current sync schedule configuration.
    /// </summary>
    [HttpGet("schedule")]
    public async Task<ActionResult<ApiResponse<SyncScheduleDto>>> GetSchedule()
    {
        var config = await _syncSchedulerService.GetScheduleConfigAsync();
        return Ok(ApiResponse<SyncScheduleDto>.SuccessResponse(config));
    }

    /// <summary>
    /// Update sync schedule configuration (cron expression, enabled, search queries).
    /// </summary>
    [HttpPut("schedule")]
    public async Task<ActionResult<ApiResponse<SyncScheduleDto>>> UpdateSchedule([FromBody] SyncScheduleConfigRequest request)
    {
        try
        {
            var config = await _syncSchedulerService.UpdateScheduleConfigAsync(request);
            return Ok(ApiResponse<SyncScheduleDto>.SuccessResponse(config, "Schedule updated successfully."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<SyncScheduleDto>.FailResponse($"Failed to update schedule: {ex.Message}"));
        }
    }

    /// <summary>
    /// Get current sync status (is any sync running, lock status for each source).
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<ApiResponse<SyncStatusOverviewDto>>> GetSyncStatus()
    {
        var sources = await _syncService.GetDataSourcesAsync();
        var sourceStatuses = sources.Select(s => _syncService.GetSyncLockStatus(s.Name)!).ToList();
        
        // For the overview dashboard, fetch only the first page with 10 items
        var recentSyncsPaged = await _syncService.GetSyncLogsAsync(1, 10);

        var status = new SyncStatusOverviewDto
        {
            IsAnySyncRunning = sourceStatuses.Any(s => s.IsLocked),
            Sources = sourceStatuses,
            RecentSyncs = recentSyncsPaged.Items.ToList()
        };

        return Ok(ApiResponse<SyncStatusOverviewDto>.SuccessResponse(status));
    }

    /// <summary>
    /// Get lock status for a specific source.
    /// </summary>
    [HttpGet("status/{sourceName}")]
    public ActionResult<ApiResponse<SyncLockStatusDto>> GetSourceLockStatus(string sourceName)
    {
        var status = _syncService.GetSyncLockStatus(sourceName);
        if (status == null)
        {
            return NotFound(ApiResponse<SyncLockStatusDto>.FailResponse($"Source '{sourceName}' not found."));
        }
        return Ok(ApiResponse<SyncLockStatusDto>.SuccessResponse(status));
    }

    /// <summary>
    /// Get sync job history from Hangfire.
    /// </summary>
    [HttpGet("schedule/history")]
    public async Task<ActionResult<ApiResponse<PagedResult<SyncJobInfoDto>>>> GetJobHistory(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var history = await _syncSchedulerService.GetJobHistoryAsync(page, pageSize);
        return Ok(ApiResponse<PagedResult<SyncJobInfoDto>>.SuccessResponse(history));
    }

    #endregion

    #region Manual Sync

    /// <summary>
    /// Manually trigger a sync (bypasses schedule).
    /// Allows selecting specific source, custom paper limit, and search query.
    /// </summary>
    /// <remarks>
    /// Request body (optional):
    /// - sourceName: "SemanticScholar", "OpenAlex", "Crossref", "ArXiv", or null for all
    /// - paperLimit: number of papers to fetch (default 10)
    /// - searchQuery: custom search query (optional)
    /// </remarks>
    [HttpPost("trigger")]
    public async Task<ActionResult<ApiResponse<ManualSyncResultDto>>> TriggerManualSync([FromBody] ManualSyncRequest? request = null)
    {
        try
        {
            var adminUserId = GetUserId();
            var result = await _syncSchedulerService.TriggerManualSyncAsync(adminUserId, request);

            if (result.Success)
            {
                return Ok(ApiResponse<ManualSyncResultDto>.SuccessResponse(result, result.Message ?? "Sync completed successfully."));
            }

            return BadRequest(ApiResponse<ManualSyncResultDto>.FailResponse(result.Message ?? "Sync failed"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<ManualSyncResultDto>.FailResponse($"Failed to trigger sync: {ex.Message}"));
        }
    }

    #endregion

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("User not authenticated.");
    }
}
