using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Hangfire;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.DTOs.GapAnalysis;
using ScholarTrend.Application.DTOs.Topics;
using ScholarTrend.Application.DTOs.TopicInsights;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Services;
using ScholarTrend.Infrastructure.Jobs;

namespace ScholarTrend.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TopicsController : ControllerBase
{
    private readonly ITopicService _topicService;
    private readonly ITopicInsightService _topicInsightService;
    private readonly IResearchGapService _researchGapService;
    private readonly IPatternMiningService _patternMiningService;
    private readonly ITrendAnalysisService _trendAnalysisService;
    private readonly ICoverageReportService _coverageReportService;
    private readonly IPaperAnalysisService _paperAnalysisService;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IGapGenerationJobTracker _gapJobTracker;

    public TopicsController(
        ITopicService topicService, 
        ITopicInsightService topicInsightService,
        IResearchGapService researchGapService,
        IPatternMiningService patternMiningService,
        ITrendAnalysisService trendAnalysisService,
        ICoverageReportService coverageReportService,
        IPaperAnalysisService paperAnalysisService,
        IBackgroundJobClient backgroundJobs,
        IGapGenerationJobTracker gapJobTracker)
    {
        _topicService = topicService;
        _topicInsightService = topicInsightService;
        _researchGapService = researchGapService;
        _patternMiningService = patternMiningService;
        _trendAnalysisService = trendAnalysisService;
        _coverageReportService = coverageReportService;
        _paperAnalysisService = paperAnalysisService;
        _backgroundJobs = backgroundJobs;
        _gapJobTracker = gapJobTracker;
    }

    /// <summary>
    /// Get all research topics.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<TopicListItemDto>>>> GetAll(
        [FromQuery] string? keyword, 
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 12)
    {
        var result = await _topicService.GetPagedAsync(keyword, page, pageSize);
        return Ok(ApiResponse<PagedResult<TopicListItemDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Get research topic details by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<TopicDetailDto>>> GetById(int id)
    {
        try
        {
            var result = await _topicService.GetByIdAsync(id);
            return Ok(ApiResponse<TopicDetailDto>.SuccessResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<TopicDetailDto>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Get topic insights dashboard.
    /// </summary>
    [HttpGet("{id:int}/insights/dashboard")]
    public async Task<ActionResult<ApiResponse<TopicInsightDashboardDto>>> GetInsightsDashboard(int id)
    {
        try
        {
            var result = await _topicInsightService.GetTopicInsightDashboardAsync(id);
            return Ok(ApiResponse<TopicInsightDashboardDto>.SuccessResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<TopicInsightDashboardDto>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Get stored research gap report for a topic (read-only, no AI).
    /// </summary>
    [HttpGet("{id:int}/gaps")]
    [Authorize(Roles = "Researcher,Admin")]
    public async Task<ActionResult<ApiResponse<ResearchGapReportDto>>> GetResearchGaps(int id)
    {
        try
        {
            var report = await _researchGapService.GetGapReportAsync(id);
            return Ok(ApiResponse<ResearchGapReportDto>.SuccessResponse(report));
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponse<ResearchGapReportDto>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Enqueue async gap generation (Hangfire). Poll GET .../gaps/jobs/{jobId}.
    /// </summary>
    [HttpPost("{id:int}/gaps/generate")]
    [Authorize(Roles = "Researcher,Admin")]
    public ActionResult<ApiResponse<GapGenerationJobDto>> EnqueueGapGeneration(
        int id,
        [FromQuery] bool force = false)
    {
        var tracked = _gapJobTracker.Register(id);
        var hangfireId = _backgroundJobs.Enqueue<ResearchGapAnalysisJob>(
            job => job.GenerateGapsForTopicTrackedAsync(id, tracked.JobId, force, CancellationToken.None));

        tracked.Message = $"Queued (Hangfire {hangfireId})";
        return Accepted(ApiResponse<GapGenerationJobDto>.SuccessResponse(tracked));
    }

    /// <summary>
    /// Get status of an async gap-generation job.
    /// </summary>
    [HttpGet("gaps/jobs/{jobId}")]
    [Authorize(Roles = "Researcher,Admin")]
    public ActionResult<ApiResponse<GapGenerationJobDto>> GetGapGenerationJob(string jobId)
    {
        var job = _gapJobTracker.Get(jobId);
        if (job == null)
            return NotFound(ApiResponse<GapGenerationJobDto>.FailResponse($"Job {jobId} not found"));
        return Ok(ApiResponse<GapGenerationJobDto>.SuccessResponse(job));
    }

    /// <summary>
    /// Latest generation job for a topic (if any, in-memory).
    /// </summary>
    [HttpGet("{id:int}/gaps/jobs/latest")]
    [Authorize(Roles = "Researcher,Admin")]
    public ActionResult<ApiResponse<GapGenerationJobDto?>> GetLatestGapGenerationJob(int id)
    {
        var job = _gapJobTracker.GetLatestForTopic(id);
        return Ok(ApiResponse<GapGenerationJobDto?>.SuccessResponse(job));
    }

    /// <summary>
    /// Get list of research gaps for a topic.
    /// </summary>
    [HttpGet("{id:int}/gaps/list")]
    public async Task<ActionResult<ApiResponse<List<ResearchGapDto>>>> ListGaps(int id)
    {
        var gaps = await _researchGapService.GetGapsAsync(id);
        return Ok(ApiResponse<List<ResearchGapDto>>.SuccessResponse(gaps));
    }

    /// <summary>
    /// Get detailed information about a specific research gap.
    /// </summary>
    [HttpGet("gaps/{gapId:int}")]
    public async Task<ActionResult<ApiResponse<ResearchGapDetailDto>>> GetGapDetail(int gapId)
    {
        var detail = await _researchGapService.GetGapDetailAsync(gapId);
        if (detail == null)
            return NotFound(ApiResponse<ResearchGapDetailDto>.FailResponse($"Gap {gapId} not found"));
        return Ok(ApiResponse<ResearchGapDetailDto>.SuccessResponse(detail));
    }

    /// <summary>
    /// Get evidence for a specific research gap.
    /// </summary>
    [HttpGet("gaps/{gapId:int}/evidences")]
    public async Task<ActionResult<ApiResponse<List<ResearchGapEvidenceDto>>>> GetGapEvidences(int gapId)
    {
        var evidences = await _researchGapService.GetGapEvidencesAsync(gapId);
        return Ok(ApiResponse<List<ResearchGapEvidenceDto>>.SuccessResponse(evidences));
    }

    /// <summary>
    /// Get mined patterns for a topic.
    /// </summary>
    [HttpGet("{id:int}/patterns")]
    public async Task<ActionResult<ApiResponse<PatternMiningResultDto>>> GetPatterns(int id)
    {
        try
        {
            var patterns = await _patternMiningService.MinePatternsAsync(id);
            return Ok(ApiResponse<PatternMiningResultDto>.SuccessResponse(patterns));
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponse<PatternMiningResultDto>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Get gap timeline for a topic.
    /// </summary>
    [HttpGet("{id:int}/trends")]
    public async Task<ActionResult<ApiResponse<GapTimelineDto>>> GetTrends(int id)
    {
        var timeline = await _trendAnalysisService.GetGapTimelineAsync(id);
        return Ok(ApiResponse<GapTimelineDto>.SuccessResponse(timeline));
    }

    /// <summary>
    /// Get coverage report for a topic.
    /// </summary>
    [HttpGet("{id:int}/coverage")]
    public async Task<ActionResult<ApiResponse<CoverageReportDto>>> GetCoverage(int id)
    {
        try
        {
            var coverage = await _coverageReportService.GenerateReportAsync(id);
            return Ok(ApiResponse<CoverageReportDto>.SuccessResponse(coverage));
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponse<CoverageReportDto>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Get paper quality report for a topic.
    /// </summary>
    [HttpGet("{id:int}/quality")]
    public async Task<ActionResult<ApiResponse<PaperQualityReportDto>>> GetQuality(int id)
    {
        var quality = await _coverageReportService.GetQualityReportAsync(id);
        return Ok(ApiResponse<PaperQualityReportDto>.SuccessResponse(quality));
    }

    /// <summary>
    /// Get paper analysis results for a topic.
    /// </summary>
    [HttpGet("{id:int}/analysis")]
    public async Task<ActionResult<ApiResponse<GapAnalysisResultDto>>> GetAnalysis(int id)
    {
        var result = await _paperAnalysisService.GetAnalysisResultAsync(id);
        return Ok(ApiResponse<GapAnalysisResultDto>.SuccessResponse(result));
    }
}
