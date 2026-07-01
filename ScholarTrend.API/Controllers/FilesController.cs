using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.DTOs.Files;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Domain.Constants;

namespace ScholarTrend.API.Controllers;

[Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Researcher}")]
[ApiController]
[Route("api/[controller]")]
[RequestSizeLimit(20 * 1024 * 1024)]
public class FilesController : ControllerBase
{
    private readonly IFileService _fileService;

    public FilesController(IFileService fileService)
    {
        _fileService = fileService;
    }

    /// <summary>
    /// Upload a file (image or document). Researcher and Admin only.
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<FileUploadResultDto>>> Upload(
        IFormFile file,
        [FromForm] string? category,
        [FromForm] string? description,
        [FromForm] int? paperId,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<FileUploadResultDto>.FailResponse("A valid file is required."));
        }

        try
        {
            var result = await _fileService.UploadAsync(
                GetUserId(),
                file.OpenReadStream(),
                file.FileName,
                file.ContentType,
                file.Length,
                category,
                description,
                paperId,
                cancellationToken);

            return Ok(ApiResponse<FileUploadResultDto>.SuccessResponse(result, "File uploaded successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<FileUploadResultDto>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// Upload or replace the current user's avatar image.
    /// </summary>
    [HttpPost("avatar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<FileUploadResultDto>>> UploadAvatar(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<FileUploadResultDto>.FailResponse("A valid image file is required."));
        }

        try
        {
            var result = await _fileService.UploadAvatarAsync(
                GetUserId(),
                file.OpenReadStream(),
                file.FileName,
                file.ContentType,
                file.Length,
                cancellationToken);

            return Ok(ApiResponse<FileUploadResultDto>.SuccessResponse(result, "Avatar uploaded successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<FileUploadResultDto>.FailResponse(ex.Message));
        }
    }

    /// <summary>
    /// List uploaded files for the current user. Admin may pass userId to inspect another user.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<FileUploadResultDto>>>> GetFiles(
        [FromQuery] FileListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _fileService.GetUserFilesAsync(
            GetUserId(),
            query,
            IsAdmin(),
            cancellationToken);

        return Ok(ApiResponse<PagedResult<FileUploadResultDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Download a previously uploaded file.
    /// </summary>
    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var result = await _fileService.DownloadAsync(id, GetUserId(), IsAdmin(), cancellationToken);
        if (result == null)
        {
            return NotFound(ApiResponse<object>.FailResponse("File not found."));
        }

        return File(result.Value.Stream, result.Value.ContentType, result.Value.FileName);
    }

    /// <summary>
    /// Soft-delete an uploaded file.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _fileService.DeleteAsync(id, GetUserId(), IsAdmin(), cancellationToken);
            return Ok(ApiResponse<object>.SuccessResponse(new { }, "File deleted successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<object>.FailResponse(ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User not authenticated.");
    }

    private bool IsAdmin()
    {
        return User.IsInRole(RoleConstants.Admin);
    }
}
