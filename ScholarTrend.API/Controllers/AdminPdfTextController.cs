using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.DTOs.Pdf;
using ScholarTrend.Application.Services;
using ScholarTrend.Domain.Constants;

namespace ScholarTrend.API.Controllers;

/// <summary>
/// Admin endpoints để trích xuất text từ PDF — phục vụ gap analysis.
/// Tất cả endpoints yêu cầu role Admin.
/// </summary>
[ApiController]
[Route("api/admin/pdf-text")]
[Authorize(Roles = RoleConstants.Admin)]
public class AdminPdfTextController : ControllerBase
{
    private readonly PdfTextExtractionService _extractionService;
    private readonly ILogger<AdminPdfTextController> _logger;

    public AdminPdfTextController(
        PdfTextExtractionService extractionService,
        ILogger<AdminPdfTextController> logger)
    {
        _extractionService = extractionService;
        _logger = logger;
    }

    /// <summary>
    /// Trích xuất text cho 1 paper cụ thể.
    /// Idempotent — cache hit (ExtractedText != null) sẽ skip trừ khi ?force=true.
    /// </summary>
    [HttpPost("papers/{researchPaperId:int}/extract")]
    public async Task<ActionResult<ApiResponse<PdfExtractionResultDto>>> ExtractForPaper(
        int researchPaperId,
        [FromQuery] bool force = false,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Admin triggered PDF text extraction for paper {Id} (force={Force})",
            researchPaperId, force);

        var result = await _extractionService.ExtractForPaperAsync(researchPaperId, forceReExtract: force, ct);
        return Ok(ApiResponse<PdfExtractionResultDto>.SuccessResponse(result));
    }

    /// <summary>
    /// Trích xuất text cho nhiều papers cùng lúc.
    /// Body: { "paperIds": [1,2,3], "force": false }
    /// </summary>
    [HttpPost("papers/extract-batch")]
    public async Task<ActionResult<ApiResponse<PdfBulkExtractionResultDto>>> ExtractForPapers(
        [FromBody] PdfExtractBatchRequest request,
        CancellationToken ct = default)
    {
        if (request.PaperIds == null || request.PaperIds.Count == 0)
        {
            return BadRequest(ApiResponse<PdfBulkExtractionResultDto>.FailResponse("paperIds cannot be empty."));
        }

        if (request.PaperIds.Count > 500)
        {
            return BadRequest(ApiResponse<PdfBulkExtractionResultDto>.FailResponse("Maximum 500 papers per batch."));
        }

        _logger.LogInformation(
            "Admin triggered batch PDF text extraction for {Count} papers (force={Force})",
            request.PaperIds.Count, request.Force);

        var result = await _extractionService.ExtractForPapersAsync(request.PaperIds, request.Force, ct);

        return Ok(ApiResponse<PdfBulkExtractionResultDto>.SuccessResponse(
            result,
            $"Batch extraction: {result.Extracted} ok, {result.Failed} failed, {result.Skipped} skipped."));
    }

    /// <summary>
    /// Backfill: trích xuất text cho TẤT CẢ papers đã download PDF (Status=Ready) mà chưa có ExtractedText.
    /// Dùng 1 lần sau khi setup PDF parser để extract toàn bộ.
    /// </summary>
    [HttpPost("backfill")]
    public async Task<ActionResult<ApiResponse<PdfBulkExtractionResultDto>>> Backfill(
        [FromQuery] int maxPapers = 200,
        CancellationToken ct = default)
    {
        if (maxPapers is < 1 or > 2000)
        {
            return BadRequest(ApiResponse<PdfBulkExtractionResultDto>.FailResponse("maxPapers must be between 1 and 2000."));
        }

        _logger.LogInformation(
            "Admin triggered PDF text extraction backfill (max={Max})", maxPapers);

        var result = await _extractionService.ExtractForAllReadyAsync(maxPapers, ct);

        return Ok(ApiResponse<PdfBulkExtractionResultDto>.SuccessResponse(
            result,
            $"Backfill: {result.Extracted} ok, {result.Failed} failed, {result.Skipped} skipped."));
    }

    /// <summary>
    /// Lấy extracted text của 1 paper (để debug/verify).
    /// </summary>
    [HttpGet("papers/{researchPaperId:int}")]
    public async Task<ActionResult<ApiResponse<object>>> GetExtractedText(int researchPaperId)
    {
        var text = await _extractionService.GetExtractedTextAsync(researchPaperId);
        if (text == null)
        {
            return NotFound(ApiResponse<object>.FailResponse($"No extracted text found for paper {researchPaperId}."));
        }

        // Truncate text nếu quá dài (chỉ preview 2000 chars)
        const int PreviewLength = 2000;
        var preview = text.Length > PreviewLength ? text[..PreviewLength] + "..." : text;

        return Ok(ApiResponse<object>.SuccessResponse(new
        {
            ResearchPaperId = researchPaperId,
            CharacterCount = text.Length,
            Preview = preview
        }));
    }
}

public class PdfExtractBatchRequest
{
    public List<int> PaperIds { get; set; } = new();
    public bool Force { get; set; } = false;
}