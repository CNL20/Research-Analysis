using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.DTOs.Reports;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Domain.Constants;

namespace ScholarTrend.API.Controllers;

[Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Researcher}")]
[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// Publication breakdown by year, keyword, topic, or journal.
    /// Optional Top ranks entities by trending score (after merging scale + trend metrics).
    /// </summary>
    [HttpGet("publications")]
    public async Task<ActionResult<ApiResponse<PublicationReportDto>>> GetPublicationReport(
        [FromQuery] ReportFilterRequest filter)
    {
        var result = await _reportService.GenerateReportAsync(filter);
        return Ok(ApiResponse<PublicationReportDto>.SuccessResponse(result));
    }

    /// <summary>
    /// Export publication report as JSON file download (same payload as GET /publications).
    /// </summary>
    [HttpGet("export/json")]
    public async Task<IActionResult> ExportJson([FromQuery] ReportFilterRequest filter)
    {
        var report = await _reportService.GenerateReportAsync(filter);
        var fileName = $"publication-report-{report.GroupBy}-{DateTime.UtcNow:yyyyMMdd}.json";
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(report, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
        });
        return File(bytes, "application/json; charset=utf-8", fileName);
    }

    /// <summary>
    /// Export publication report as CSV file download (same fields as GET /publications).
    /// </summary>
    [HttpGet("export/csv")]
    public async Task<IActionResult> ExportCsv([FromQuery] ReportFilterRequest filter)
    {
        var report = await _reportService.GenerateReportAsync(filter);
        var fileName = $"publication-report-{report.GroupBy}-{DateTime.UtcNow:yyyyMMdd}.csv";
        var bytes = _reportService.ExportCsv(report);
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }
}
