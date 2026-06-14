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

    public AdminSyncController(ISyncService syncService)
    {
        _syncService = syncService;
    }

    /// <summary>
    /// Fetch papers from external APIs and create a pending sync proposal (does not import until approved).
    /// </summary>
    [HttpPost("trigger")]
    public async Task<ActionResult<ApiResponse<SyncResultDto>>> TriggerSync([FromBody] TriggerSyncRequest? request)
    {
        try
        {
            var result = await _syncService.RunSyncAsync(request?.SourceName);
            return Ok(ApiResponse<SyncResultDto>.SuccessResponse(result, "Sync proposal created successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SyncResultDto>.FailResponse(ex.Message));
        }
    }

    [HttpGet("pending")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SyncProposalListItemDto>>>> GetPendingProposals([FromQuery] int limit = 50)
    {
        var result = await _syncService.GetPendingProposalsAsync(limit);
        return Ok(ApiResponse<IReadOnlyList<SyncProposalListItemDto>>.SuccessResponse(result));
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
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SyncLogDto>>>> GetLogs([FromQuery] int limit = 50)
    {
        var result = await _syncService.GetSyncLogsAsync(limit);
        return Ok(ApiResponse<IReadOnlyList<SyncLogDto>>.SuccessResponse(result));
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

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("User not authenticated.");
    }
}
