using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarTrend.Application.DTOs.Aggregation;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.DTOs.Papers;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Constants;

namespace ScholarTrend.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PapersController : ControllerBase
{
    private readonly IPaperService _paperService;
    private readonly IPaperAggregationService _paperAggregationService;
    private readonly IPdfAnalysisService _pdfAnalysisService;
    private readonly IPaperPdfFileRepository _paperPdfFileRepository;
    private readonly IPaperFileStorageProvider _paperFileStorageProvider;
    private readonly ILogger<PapersController> _logger;

    public PapersController(
        IPaperService paperService,
        IPaperAggregationService paperAggregationService,
        IPdfAnalysisService pdfAnalysisService,
        IPaperPdfFileRepository paperPdfFileRepository,
        IPaperFileStorageProvider paperFileStorageProvider,
        ILogger<PapersController> logger)
    {
        _paperService = paperService;
        _paperAggregationService = paperAggregationService;
        _pdfAnalysisService = pdfAnalysisService;
        _paperPdfFileRepository = paperPdfFileRepository;
        _paperFileStorageProvider = paperFileStorageProvider;
        _logger = logger;
    }

    /// <summary>
    /// Aggregate metadata for a paper from multiple bibliographic sources by DOI.
    /// </summary>
    [HttpGet("aggregate")]
    public async Task<ActionResult<ApiResponse<PaperAggregateResultDto>>> AggregateByDoi([FromQuery] string doi)
    {
        try
        {
            var result = await _paperAggregationService.AggregateByDoiAsync(doi);
            return Ok(ApiResponse<PaperAggregateResultDto>.SuccessResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<PaperAggregateResultDto>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Aggregate metadata for a stored paper from multiple bibliographic sources.
    /// </summary>
    [HttpGet("{id:int}/aggregate")]
    public async Task<ActionResult<ApiResponse<PaperAggregateResultDto>>> AggregateByPaperId(int id)
    {
        try
        {
            var result = await _paperAggregationService.AggregateByPaperIdAsync(id);
            return Ok(ApiResponse<PaperAggregateResultDto>.SuccessResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<PaperAggregateResultDto>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Search research papers by keyword, title, author, journal, publish year, or all fields with optional filters.
    /// Use SortBy=newest (or id) with empty Query to list all browsable papers, newest approved/imported first.
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<ApiResponse<PagedResult<PaperListItemDto>>>> Search([FromQuery] PaperSearchRequest request)
    {
        var userId = GetUserId();
        var result = await _paperService.SearchAsync(request, userId);
        return Ok(ApiResponse<PagedResult<PaperListItemDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Get paper details by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<PaperDetailDto>>> GetById(int id)
    {
        try
        {
            var result = await _paperService.GetByIdAsync(id, GetUserId());
            return Ok(ApiResponse<PaperDetailDto>.SuccessResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<PaperDetailDto>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Record a view for a paper.
    /// </summary>
    [HttpPost("{id:int}/view")]
    public async Task<ActionResult<ApiResponse<object>>> RecordView(int id)
    {
        try
        {
            await _paperService.RecordViewAsync(id);
            return Ok(ApiResponse<object>.SuccessResponse(null, "View recorded successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<object>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Analyze a paper using AI to extract limitations, gap-statements, and future work from its PDF.
    /// Reads the PDF directly for richer analysis. Falls back to "PDF không tồn tại" / "PDF gặp trục trặc".
    /// Only available to Researcher and Admin roles (premium feature).
    /// </summary>
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Researcher}")]
    [HttpPost("{id:int}/analyze")]
    public async Task<ActionResult<ApiResponse<PaperAnalysisResultDto>>> AnalyzePaper(int id)
    {
        try
        {
            var extraction = await _pdfAnalysisService.AnalyzePdfAsync(id);

            if (extraction == null)
            {
                var paperCheck = await _paperService.GetByIdAsync(id, GetUserId());
                if (paperCheck == null)
                    return NotFound(ApiResponse<PaperAnalysisResultDto>.FailResponse("Paper not found."));

                if (string.IsNullOrWhiteSpace(paperCheck.PdfUrl))
                    return BadRequest(ApiResponse<PaperAnalysisResultDto>.FailResponse("PDF không tồn tại"));

                return StatusCode(503,
                    ApiResponse<PaperAnalysisResultDto>.FailResponse("PDF gặp trục trặc. Vui lòng thử lại sau."));
            }

            var paper = await _paperService.GetByIdAsync(id, GetUserId());

            var result = new PaperAnalysisResultDto
            {
                PaperId = id,
                Title = paper?.Title ?? "",
                Limitations = extraction.Limitations,
                FutureWork = extraction.FutureWork,
                WasInferred = extraction.Limitations.Any(l => l.Contains("[AI Inferred]")) ||
                              extraction.FutureWork.Any(f => f.Contains("[AI Inferred]"))
            };

            return Ok(ApiResponse<PaperAnalysisResultDto>.SuccessResponse(result, "Paper analyzed successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<PaperAnalysisResultDto>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Get papers by research topic with pagination.
    /// </summary>
    [HttpGet("by-topic/{topicId:int}")]
    public async Task<ActionResult<ApiResponse<PagedResult<PaperListItemDto>>>> GetByTopic(
        int topicId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var result = await _paperService.GetByTopicAsync(topicId, page, pageSize);
            return Ok(ApiResponse<PagedResult<PaperListItemDto>>.SuccessResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<PagedResult<PaperListItemDto>>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Get papers by journal with pagination.
    /// </summary>
    [HttpGet("by-journal/{journalId:int}")]
    public async Task<ActionResult<ApiResponse<PagedResult<PaperListItemDto>>>> GetByJournal(
        int journalId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var result = await _paperService.GetByJournalAsync(journalId, page, pageSize);
            return Ok(ApiResponse<PagedResult<PaperListItemDto>>.SuccessResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<PagedResult<PaperListItemDto>>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Get recent search history for the current user.
    /// </summary>
    [HttpGet("search-history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SearchHistoryDto>>>> GetSearchHistory([FromQuery] int limit = 20)
    {
        var result = await _paperService.GetSearchHistoryAsync(GetUserId(), limit);
        return Ok(ApiResponse<IReadOnlyList<SearchHistoryDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Tải PDF file của một bài báo. Proxy qua backend (không trả URL B2 trực tiếp vì bucket private).
    /// Trả 404 nếu paper không có PDF (chưa tải về, hoặc tải thất bại).
    /// </summary>
    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> DownloadPdf(int id, CancellationToken ct)
    {
        var pdfRecord = await _paperPdfFileRepository.GetByResearchPaperIdAsync(id);
        if (pdfRecord is null)
        {
            return NotFound(ApiResponse<object>.FailResponse("PDF not available for this paper."));
        }

        if (pdfRecord.Status != PaperDownloadStatus.Ready)
        {
            return NotFound(ApiResponse<object>.FailResponse(
                $"PDF is not ready (current status: {pdfRecord.Status}). Please try again later."));
        }

        Stream? stream;
        try
        {
            var storage = _paperFileStorageProvider.GetActiveStorage();
            stream = await storage.OpenReadAsync(pdfRecord.LocalRelativePath, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open PDF stream for paper {Id}", id);
            return StatusCode(500, ApiResponse<object>.FailResponse("Failed to retrieve PDF."));
        }

        if (stream is null)
        {
            return NotFound(ApiResponse<object>.FailResponse("PDF file not found in storage."));
        }

        var contentType = pdfRecord.ContentType ?? "application/pdf";
        var fileName = $"paper-{id}.pdf";
        return File(stream, contentType, fileName);
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User not authenticated.");
    }
}
