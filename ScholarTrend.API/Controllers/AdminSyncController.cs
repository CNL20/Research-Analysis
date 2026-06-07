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

    [HttpPost("trigger")]
    public async Task<ActionResult<ApiResponse<SyncResultDto>>> TriggerSync([FromBody] TriggerSyncRequest? request)
    {
        try
        {
            var result = await _syncService.RunSyncAsync(request?.SourceName);
            return Ok(ApiResponse<SyncResultDto>.SuccessResponse(result, "Sync triggered successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SyncResultDto>.FailResponse(ex.Message));
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
}
