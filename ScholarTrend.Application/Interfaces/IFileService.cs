using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.DTOs.Files;

namespace ScholarTrend.Application.Interfaces;

public interface IFileService
{
    Task<FileUploadResultDto> UploadAsync(
        string userId,
        Stream content,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string? category,
        string? description,
        int? paperId,
        CancellationToken cancellationToken = default);

    Task<FileUploadResultDto> UploadAvatarAsync(
        string userId,
        Stream content,
        string originalFileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default);

    Task<PagedResult<FileUploadResultDto>> GetUserFilesAsync(
        string userId,
        FileListQuery query,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<(Stream Stream, string ContentType, string FileName)?> DownloadAsync(
        int fileId,
        string userId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(int fileId, string userId, bool isAdmin, CancellationToken cancellationToken = default);
}
