using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.DTOs.Trends;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Domain.Constants;

namespace ScholarTrend.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TrendsController : ControllerBase
{
    private readonly ITrendService _trendService;

    public TrendsController(ITrendService trendService)
    {
        _trendService = trendService;
    }

    /// <summary>
    /// Trending dashboard with top keywords, topics, journals and publication chart data.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<TrendDashboardDto>>> GetDashboard([FromQuery] TrendFilterRequest filter)
    {
        var result = await _trendService.GetDashboardAsync(filter);
        return Ok(ApiResponse<TrendDashboardDto>.SuccessResponse(result));
    }

    /// <summary>
    /// Keyword trend time-series for line charts.
    /// </summary>
    [HttpGet("keywords")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TrendSeriesDto>>>> GetKeywordTrends([FromQuery] TrendFilterRequest filter)
    {
        var result = await _trendService.GetKeywordTrendsAsync(filter);
        return Ok(ApiResponse<IReadOnlyList<TrendSeriesDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Top trending keywords by TrendingScore in the latest period.
    /// </summary>
    [HttpGet("keywords/top")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TopTrendItemDto>>>> GetTopKeywords([FromQuery] TrendFilterRequest filter)
    {
        var result = await _trendService.GetTopKeywordsAsync(filter);
        return Ok(ApiResponse<IReadOnlyList<TopTrendItemDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Research topic trend time-series for line charts.
    /// </summary>
    [HttpGet("topics")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TrendSeriesDto>>>> GetTopicTrends([FromQuery] TrendFilterRequest filter)
    {
        var result = await _trendService.GetTopicTrendsAsync(filter);
        return Ok(ApiResponse<IReadOnlyList<TrendSeriesDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Top trending research topics.
    /// </summary>
    [HttpGet("topics/top")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TopTrendItemDto>>>> GetTopTopics([FromQuery] TrendFilterRequest filter)
    {
        var result = await _trendService.GetTopTopicsAsync(filter);
        return Ok(ApiResponse<IReadOnlyList<TopTrendItemDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Journal trend time-series for line charts.
    /// </summary>
    [HttpGet("journals")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TrendSeriesDto>>>> GetJournalTrends([FromQuery] TrendFilterRequest filter)
    {
        var result = await _trendService.GetJournalTrendsAsync(filter);
        return Ok(ApiResponse<IReadOnlyList<TrendSeriesDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Top trending journals.
    /// </summary>
    [HttpGet("journals/top")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TopTrendItemDto>>>> GetTopJournals([FromQuery] TrendFilterRequest filter)
    {
        var result = await _trendService.GetTopJournalsAsync(filter);
        return Ok(ApiResponse<IReadOnlyList<TopTrendItemDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Overall publication trend aggregated from all papers.
    /// </summary>
    [HttpGet("publications")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TrendDataPointDto>>>> GetPublicationTrend([FromQuery] TrendFilterRequest filter)
    {
        var result = await _trendService.GetPublicationTrendAsync(filter);
        return Ok(ApiResponse<IReadOnlyList<TrendDataPointDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Compare 2–3 keyword, topic, or journal trends on the same chart. Researcher and Admin only.
    /// </summary>
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Researcher}")]
    [HttpPost("compare")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TrendSeriesDto>>>> CompareTrends([FromBody] TrendCompareRequest request)
    {
        try
        {
            var result = await _trendService.CompareTrendsAsync(request);
            return Ok(ApiResponse<IReadOnlyList<TrendSeriesDto>>.SuccessResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<IReadOnlyList<TrendSeriesDto>>.FailResponse(ex.Message));
        }
    }
}
