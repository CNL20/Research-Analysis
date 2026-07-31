using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.DTOs.Migration;
using ScholarTrend.Application.Services;
using ScholarTrend.Domain.Constants;
using ScholarTrend.Infrastructure.Services;

namespace ScholarTrend.API.Controllers;

/// <summary>
/// Admin tool: chạy data migration thủ công. Tất cả endpoint đều Admin-only.
/// </summary>
[ApiController]
[Route("api/admin/migrations")]
[Authorize(Roles = RoleConstants.Admin)]
public class AdminMigrationController : ControllerBase
{
    private readonly PdfStorageMigrationService _migrationService;
    private readonly PdfStorageStatusService _statusService;
    private readonly ILogger<AdminMigrationController> _logger;

    public AdminMigrationController(
        PdfStorageMigrationService migrationService,
        PdfStorageStatusService statusService,
        ILogger<AdminMigrationController> logger)
    {
        _migrationService = migrationService;
        _statusService = statusService;
        _logger = logger;
    }

    /// <summary>
    /// Chuyển PDFs đã có ở local storage (uploads/papers/) lên Backblaze B2.
    /// Scan tất cả PaperPdfFile có Status=Ready, upload từng file lên B2 (idempotent).
    /// </summary>
    [HttpPost("pdfs/local-to-b2")]
    public async Task<ActionResult<ApiResponse<PdfMigrationResultDto>>> MigratePdfsToB2(CancellationToken ct)
    {
        _logger.LogInformation("Admin triggered PDF storage migration (Local → B2)");

        try
        {
            var result = await _migrationService.MigrateAsync(ct);
            return Ok(ApiResponse<PdfMigrationResultDto>.SuccessResponse(
                result,
                $"Migration completed: {result.SuccessCount} ok, {result.FailureCount} failed, {result.SkippedCount} skipped in {result.ElapsedMs} ms."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF migration failed");
            return StatusCode(500, ApiResponse<PdfMigrationResultDto>.FailResponse(
                $"Migration failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Danh sách PDFs (phân trang) + tổng hợp theo status.
    /// </summary>
    [HttpGet("pdfs")]
    public async Task<ActionResult<ApiResponse<PdfStorageListingDto>>> ListPdfs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        // Backward compatible: old clients passed ?limit=N (first page only).
        if (limit is > 0 && page == 1 && pageSize == 20)
        {
            pageSize = Math.Clamp(limit.Value, 1, 100);
        }

        var (items, totalCount) = await _statusService.GetPagedAsync(page, pageSize, search, status, ct);
        var summary = await _statusService.GetStatusSummaryAsync(ct);

        return Ok(ApiResponse<PdfStorageListingDto>.SuccessResponse(
            new PdfStorageListingDto
            {
                TotalCount = totalCount,
                Page = page < 1 ? 1 : page,
                PageSize = pageSize,
                StatusSummary = summary,
                Items = items
            }));
    }
}