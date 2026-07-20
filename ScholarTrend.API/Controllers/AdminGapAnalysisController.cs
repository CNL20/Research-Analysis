using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Infrastructure.Jobs;

namespace ScholarTrend.API.Controllers;

[ApiController]
[Route("api/admin/gap-analysis")]
[Authorize(Roles = "Admin")]
public class AdminGapAnalysisController : ControllerBase
{
    private readonly PaperQualityAssessmentJob _qualityJob;
    private readonly PaperAnalysisExtractionJob _extractionJob;
    private readonly PatternMiningJob _patternJob;
    private readonly ResearchGapAnalysisJob _gapJob;
    private readonly ILogger<AdminGapAnalysisController> _logger;

    public AdminGapAnalysisController(
        PaperQualityAssessmentJob qualityJob,
        PaperAnalysisExtractionJob extractionJob,
        PatternMiningJob patternJob,
        ResearchGapAnalysisJob gapJob,
        ILogger<AdminGapAnalysisController> logger)
    {
        _qualityJob = qualityJob;
        _extractionJob = extractionJob;
        _patternJob = patternJob;
        _gapJob = gapJob;
        _logger = logger;
    }

    /// <summary>
    /// Trigger quality assessment for all papers.
    /// </summary>
    [HttpPost("quality/assess")]
    public async Task<ActionResult<ApiResponse<string>>> TriggerQualityAssessment()
    {
        _logger.LogInformation("Admin triggered quality assessment job");
        await _qualityJob.AssessAllPapersAsync();
        return Ok(ApiResponse<string>.SuccessResponse("Quality assessment job started"));
    }

    /// <summary>
    /// Trigger quality assessment for a specific topic.
    /// </summary>
    [HttpPost("quality/assess/{topicId:int}")]
    public async Task<ActionResult<ApiResponse<string>>> TriggerTopicQualityAssessment(int topicId)
    {
        _logger.LogInformation("Admin triggered quality assessment for topic {TopicId}", topicId);
        await _qualityJob.AssessTopicPapersAsync(topicId);
        return Ok(ApiResponse<string>.SuccessResponse($"Quality assessment for topic {topicId} started"));
    }

    /// <summary>
    /// Trigger paper analysis extraction for all topics.
    /// </summary>
    [HttpPost("extract")]
    public async Task<ActionResult<ApiResponse<string>>> TriggerExtraction()
    {
        _logger.LogInformation("Admin triggered paper analysis extraction job");
        await _extractionJob.RunExtractionAsync();
        return Ok(ApiResponse<string>.SuccessResponse("Paper analysis extraction job started"));
    }

    /// <summary>
    /// Trigger paper analysis extraction for a specific topic.
    /// </summary>
    [HttpPost("extract/{topicId:int}")]
    public async Task<ActionResult<ApiResponse<string>>> TriggerTopicExtraction(int topicId)
    {
        _logger.LogInformation("Admin triggered paper analysis extraction for topic {TopicId}", topicId);
        await _extractionJob.ExtractForTopicAsync(topicId);
        return Ok(ApiResponse<string>.SuccessResponse($"Paper analysis extraction for topic {topicId} started"));
    }

    /// <summary>
    /// Trigger pattern mining for all topics.
    /// </summary>
    [HttpPost("patterns/mine")]
    public async Task<ActionResult<ApiResponse<string>>> TriggerPatternMining()
    {
        _logger.LogInformation("Admin triggered pattern mining job");
        await _patternJob.MineAllTopicsAsync();
        return Ok(ApiResponse<string>.SuccessResponse("Pattern mining job started"));
    }

    /// <summary>
    /// Trigger pattern mining for a specific topic.
    /// </summary>
    [HttpPost("patterns/mine/{topicId:int}")]
    public async Task<ActionResult<ApiResponse<string>>> TriggerTopicPatternMining(int topicId)
    {
        _logger.LogInformation("Admin triggered pattern mining for topic {TopicId}", topicId);
        await _patternJob.MineTopicPatternsAsync(topicId);
        return Ok(ApiResponse<string>.SuccessResponse($"Pattern mining for topic {topicId} started"));
    }

    /// <summary>
    /// Trigger research gap generation for all topics.
    /// </summary>
    [HttpPost("gaps/generate")]
    public async Task<ActionResult<ApiResponse<string>>> TriggerGapGeneration()
    {
        _logger.LogInformation("Admin triggered research gap generation job");
        await _gapJob.RunScheduledGenerationAsync();
        return Ok(ApiResponse<string>.SuccessResponse("Research gap generation job started"));
    }

    /// <summary>
    /// Trigger research gap generation for a specific topic.
    /// </summary>
    [HttpPost("gaps/generate/{topicId:int}")]
    public async Task<ActionResult<ApiResponse<string>>> TriggerTopicGapGeneration(int topicId)
    {
        _logger.LogInformation("Admin triggered research gap generation for topic {TopicId}", topicId);
        await _gapJob.GenerateGapsForTopicAsync(topicId);
        return Ok(ApiResponse<string>.SuccessResponse($"Research gap generation for topic {topicId} started"));
    }

    /// <summary>
    /// Regenerate research gaps for a specific topic (deletes existing and generates new).
    /// </summary>
    [HttpPost("gaps/regenerate/{topicId:int}")]
    public async Task<ActionResult<ApiResponse<string>>> RegenerateTopicGaps(int topicId)
    {
        _logger.LogInformation("Admin triggered research gap regeneration for topic {TopicId}", topicId);
        await _gapJob.RegenerateGapsAsync(topicId);
        return Ok(ApiResponse<string>.SuccessResponse($"Research gap regeneration for topic {topicId} completed"));
    }

    /// <summary>
    /// Run full pipeline for a topic: quality assessment -> extraction -> pattern mining -> gap generation.
    /// </summary>
    [HttpPost("pipeline/{topicId:int}")]
    public async Task<ActionResult<ApiResponse<string>>> RunFullPipeline(int topicId)
    {
        _logger.LogInformation("Admin triggered full pipeline for topic {TopicId}", topicId);
        
        await _qualityJob.AssessTopicPapersAsync(topicId);
        await _extractionJob.ExtractForTopicAsync(topicId);
        await _patternJob.MineTopicPatternsAsync(topicId);
        await _gapJob.GenerateGapsForTopicAsync(topicId);
        
        return Ok(ApiResponse<string>.SuccessResponse($"Full pipeline for topic {topicId} completed"));
    }
}
