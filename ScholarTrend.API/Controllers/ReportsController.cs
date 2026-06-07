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
    /// Generate publication report grouped by year, keyword, or topic.
    /// </summary>
    [HttpGet("publications")]
    public async Task<ActionResult<ApiResponse<PublicationReportDto>>> GetPublicationReport(
        [FromQuery] ReportFilterRequest filter)
    {
        var result = await _reportService.GenerateReportAsync(filter);
        return Ok(ApiResponse<PublicationReportDto>.SuccessResponse(result));
    }

    /// <summary>
    /// Export publication report as JSON file download.
    /// </summary>
    [HttpGet("export/json")]
    public async Task<IActionResult> ExportJson([FromQuery] ReportFilterRequest filter)
    {
        var report = await _reportService.GenerateReportAsync(filter);
        var fileName = $"publication-report-{report.GroupBy}-{DateTime.UtcNow:yyyyMMdd}.json";
        return File(
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            }),
            "application/json",
            fileName);
    }

    /// <summary>
    /// Export publication report as CSV file download.
    /// </summary>
    [HttpGet("export/csv")]
    public async Task<IActionResult> ExportCsv([FromQuery] ReportFilterRequest filter)
    {
        var report = await _reportService.GenerateReportAsync(filter);
        var fileName = $"publication-report-{report.GroupBy}-{DateTime.UtcNow:yyyyMMdd}.csv";
        var bytes = _reportService.ExportCsv(report);
        return File(bytes, "text/csv", fileName);
    }
}
