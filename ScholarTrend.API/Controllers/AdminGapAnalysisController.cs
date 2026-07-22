using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Infrastructure.Data;
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
    private readonly IAiExtractionService _aiExtractionService;
    private readonly ScholarTrendDbContext _context;
    private readonly ILogger<AdminGapAnalysisController> _logger;

    public AdminGapAnalysisController(
        PaperQualityAssessmentJob qualityJob,
        PaperAnalysisExtractionJob extractionJob,
        PatternMiningJob patternJob,
        ResearchGapAnalysisJob gapJob,
        IAiExtractionService aiExtractionService,
        ScholarTrendDbContext context,
        ILogger<AdminGapAnalysisController> logger)
    {
        _qualityJob = qualityJob;
        _extractionJob = extractionJob;
        _patternJob = patternJob;
        _gapJob = gapJob;
        _aiExtractionService = aiExtractionService;
        _context = context;
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

    /// <summary>
    /// Force extract analysis for a specific paper (bypass existing checks).
    /// </summary>
    [HttpPost("extract-paper/{paperId:int}")]
    public async Task<ActionResult<ApiResponse<PaperExtractionResultDto>>> ExtractPaper(int paperId)
    {
        try
        {
            var paper = await _context.ResearchPapers.FindAsync(paperId);
            if (paper == null)
                return NotFound(ApiResponse<PaperExtractionResultDto>.FailResponse($"Paper {paperId} not found"));

            if (string.IsNullOrWhiteSpace(paper.Abstract))
            {
                return BadRequest(ApiResponse<PaperExtractionResultDto>.FailResponse("Paper has no abstract"));
            }

            _logger.LogInformation("Force extracting analysis for paper {PaperId}: {Title}", paperId, paper.Title);

            // Call AI extraction service
            var extraction = await _aiExtractionService.ExtractFromAbstractAsync(paper.Abstract, CancellationToken.None);

            if (extraction == null)
            {
                return BadRequest(ApiResponse<PaperExtractionResultDto>.FailResponse("AI extraction returned null - check API key and quota"));
            }

            // Get or check quality
            var quality = await _context.PaperQualities.FirstOrDefaultAsync(q => q.PaperId == paperId);

            // Calculate confidence
            int confidence = 50;
            if (extraction.Methods?.Any() == true) confidence += 10;
            if (extraction.Datasets?.Any() == true) confidence += 10;
            if (extraction.Limitations?.Any() == true) confidence += 10;
            if (extraction.FutureWork?.Any() == true) confidence += 10;
            if (!string.IsNullOrEmpty(extraction.ResearchProblem)) confidence += 5;
            if (!string.IsNullOrEmpty(extraction.Metric)) confidence += 5;
            confidence = Math.Min(confidence, 100);

            // Check if analysis already exists
            var existingAnalysis = await _context.PaperAnalyses.FirstOrDefaultAsync(a => a.PaperId == paperId);
            if (existingAnalysis != null)
            {
                // Update existing
                existingAnalysis.ResearchProblem = extraction.ResearchProblem;
                existingAnalysis.Method = extraction.Methods?.FirstOrDefault();
                existingAnalysis.Dataset = extraction.Datasets?.FirstOrDefault();
                existingAnalysis.Metric = extraction.Metric;
                existingAnalysis.Contribution = extraction.Contribution;
                existingAnalysis.MethodsJson = System.Text.Json.JsonSerializer.Serialize(extraction.Methods ?? new List<string>());
                existingAnalysis.DatasetsJson = System.Text.Json.JsonSerializer.Serialize(extraction.Datasets ?? new List<string>());
                existingAnalysis.LimitationsJson = System.Text.Json.JsonSerializer.Serialize(extraction.Limitations ?? new List<string>());
                existingAnalysis.FutureWorkJson = System.Text.Json.JsonSerializer.Serialize(extraction.FutureWork ?? new List<string>());
                existingAnalysis.DiscussionsJson = System.Text.Json.JsonSerializer.Serialize(extraction.Discussions ?? new List<string>());
                existingAnalysis.ConclusionsJson = System.Text.Json.JsonSerializer.Serialize(extraction.Conclusions ?? new List<string>());
                existingAnalysis.Confidence = confidence;
                existingAnalysis.AnalysisLevel = quality?.HasPdf == true ? "Hybrid" : "Abstract";
                existingAnalysis.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Create new
                var analysis = new Domain.Entities.PaperAnalysis
                {
                    PaperId = paperId,
                    ResearchProblem = extraction.ResearchProblem,
                    Method = extraction.Methods?.FirstOrDefault(),
                    Dataset = extraction.Datasets?.FirstOrDefault(),
                    Metric = extraction.Metric,
                    Contribution = extraction.Contribution,
                    MethodsJson = System.Text.Json.JsonSerializer.Serialize(extraction.Methods ?? new List<string>()),
                    DatasetsJson = System.Text.Json.JsonSerializer.Serialize(extraction.Datasets ?? new List<string>()),
                    LimitationsJson = System.Text.Json.JsonSerializer.Serialize(extraction.Limitations ?? new List<string>()),
                    FutureWorkJson = System.Text.Json.JsonSerializer.Serialize(extraction.FutureWork ?? new List<string>()),
                    DiscussionsJson = System.Text.Json.JsonSerializer.Serialize(extraction.Discussions ?? new List<string>()),
                    ConclusionsJson = System.Text.Json.JsonSerializer.Serialize(extraction.Conclusions ?? new List<string>()),
                    Confidence = confidence,
                    AnalysisLevel = quality?.HasPdf == true ? "Hybrid" : "Abstract",
                    AnalysisSource = "Groq",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _context.PaperAnalyses.AddAsync(analysis);
            }

            await _context.SaveChangesAsync();

            var result = new PaperExtractionResultDto
            {
                PaperId = paperId,
                Title = paper.Title,
                Extracted = true,
                Confidence = confidence,
                ResearchProblem = extraction.ResearchProblem ?? "",
                Methods = extraction.Methods ?? new List<string>(),
                Datasets = extraction.Datasets ?? new List<string>(),
                Limitations = extraction.Limitations ?? new List<string>(),
                FutureWork = extraction.FutureWork ?? new List<string>(),
                Message = "Extraction successful"
            };

            return Ok(ApiResponse<PaperExtractionResultDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error force extracting paper {PaperId}", paperId);
            return BadRequest(ApiResponse<PaperExtractionResultDto>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Test AI extraction service with a sample abstract.
    /// </summary>
    [HttpGet("test-ai")]
    public async Task<ActionResult<ApiResponse<string>>> TestAiService()
    {
        try
        {
            var sampleAbstract = "We propose a novel deep learning approach using Graph Neural Networks (GNN) for recommendation systems. Our method leverages neighborhood aggregation and attention mechanisms to improve recommendation accuracy on sparse datasets. We evaluate on MovieLens and Pinterest datasets, achieving 15% improvement over baseline methods. However, our approach has limitations in handling cold-start users and computational complexity for large-scale graphs.";

            var extraction = await _aiExtractionService.ExtractFromAbstractAsync(sampleAbstract, CancellationToken.None);

            if (extraction == null)
            {
                return BadRequest(ApiResponse<string>.FailResponse(
                    "AI extraction returned NULL. Possible causes:\n" +
                    "1. API key not configured or invalid\n" +
                    "2. API quota exceeded\n" +
                    "3. Network connectivity issues\n" +
                    "4. Rate limiting from Groq API"));
            }

            return Ok(ApiResponse<string>.SuccessResponse(
                $"AI service working! Extracted:\n" +
                $"- Method: {extraction.Methods.FirstOrDefault() ?? "N/A"}\n" +
                $"- Dataset: {extraction.Datasets.FirstOrDefault() ?? "N/A"}\n" +
                $"- Metric: {extraction.Metric ?? "N/A"}\n" +
                $"- Research Problem: {extraction.ResearchProblem ?? "N/A"}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI service test failed");
            return BadRequest(ApiResponse<string>.FailResponse(
                $"AI service error: {ex.Message}\n\n" +
                "Check:\n" +
                "1. GROQ_API_KEY in appsettings.json\n" +
                "2. Internet connectivity\n" +
                "3. Groq API status at status.groq.com"));
        }
    }
}

/// <summary>
/// DTO for paper extraction result response.
/// </summary>
public class PaperExtractionResultDto
{
    public int PaperId { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool Extracted { get; set; }
    public int Confidence { get; set; }
    public string ResearchProblem { get; set; } = string.Empty;
    public List<string> Methods { get; set; } = new();
    public List<string> Datasets { get; set; } = new();
    public List<string> Limitations { get; set; } = new();
    public List<string> FutureWork { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}
