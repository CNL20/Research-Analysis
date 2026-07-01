using Microsoft.Extensions.Options;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.DTOs.Files;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Application.Options;
using ScholarTrend.Domain.Constants;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Services;

public class FileService : IFileService
{
    private static readonly Dictionary<string, string> ContentTypeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["application/pdf"] = ".pdf",
        ["text/csv"] = ".csv",
        ["application/json"] = ".json"
    };

    private readonly IUserFileRepository _userFiles;
    private readonly IResearchPaperRepository _papers;
    private readonly IFileStorageService _storage;
    private readonly FileUploadSettings _settings;

    public FileService(
        IUserFileRepository userFiles,
        IResearchPaperRepository papers,
        IFileStorageService storage,
        IOptions<FileUploadSettings> options)
    {
        _userFiles = userFiles;
        _papers = papers;
        _storage = storage;
        _settings = options.Value;
    }

    public Task<FileUploadResultDto> UploadAsync(
        string userId,
        Stream content,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string? category,
        string? description,
        int? paperId,
        CancellationToken cancellationToken = default)
    {
        return UploadInternalAsync(
            userId,
            content,
            originalFileName,
            contentType,
            sizeBytes,
            category,
            description,
            paperId,
            replaceExistingAvatars: false,
            cancellationToken);
    }

    public Task<FileUploadResultDto> UploadAvatarAsync(
        string userId,
        Stream content,
        string originalFileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        return UploadInternalAsync(
            userId,
            content,
            originalFileName,
            contentType,
            sizeBytes,
            FileCategories.Avatar,
            null,
            null,
            replaceExistingAvatars: true,
            cancellationToken);
    }

    public async Task<PagedResult<FileUploadResultDto>> GetUserFilesAsync(
        string userId,
        FileListQuery query,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var targetUserId = isAdmin && !string.IsNullOrWhiteSpace(query.UserId)
            ? query.UserId
            : userId;

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;
        var category = NormalizeCategory(query.Category);

        var (items, total) = await _userFiles.GetUserFilesAsync(targetUserId, category, page, pageSize);

        return new PagedResult<FileUploadResultDto>
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<(Stream Stream, string ContentType, string FileName)?> DownloadAsync(
        int fileId,
        string userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var file = await _userFiles.GetByIdAsync(fileId);
        if (file == null || (!isAdmin && file.UserId != userId))
        {
            return null;
        }

        var stream = await _storage.OpenReadAsync(file.UserId, file.StoredFileName, cancellationToken);
        return (stream, file.ContentType, file.OriginalFileName);
    }

    public async Task DeleteAsync(int fileId, string userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var file = await _userFiles.GetByIdAsync(fileId);
        if (file == null)
        {
            throw new InvalidOperationException("File not found.");
        }

        if (!isAdmin && file.UserId != userId)
        {
            throw new UnauthorizedAccessException("You do not have permission to delete this file.");
        }

        file.IsDeleted = true;
        await _userFiles.UpdateAsync(file);
        await _storage.DeleteAsync(file.UserId, file.StoredFileName, cancellationToken);
    }

    private async Task<FileUploadResultDto> UploadInternalAsync(
        string userId,
        Stream content,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string? category,
        string? description,
        int? paperId,
        bool replaceExistingAvatars,
        CancellationToken cancellationToken)
    {
        if (content == null || sizeBytes <= 0 || string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new InvalidOperationException("A valid file is required.");
        }

        var normalizedCategory = NormalizeCategory(category) ?? InferCategory(contentType);
        ValidateCategory(normalizedCategory);
        ValidateContentType(normalizedCategory, contentType);
        ValidateSize(normalizedCategory, sizeBytes);

        if (paperId.HasValue)
        {
            var paper = await _papers.GetPaperWithDetailsAsync(paperId.Value);
            if (paper == null)
            {
                throw new InvalidOperationException("Linked paper was not found.");
            }
        }

        if (!replaceExistingAvatars)
        {
            var activeCount = await _userFiles.CountActiveByUserAsync(userId);
            if (activeCount >= _settings.MaxFilesPerUser)
            {
                throw new InvalidOperationException($"You can upload at most {_settings.MaxFilesPerUser} files.");
            }
        }

        var extension = GetExtension(originalFileName, contentType);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";

        if (replaceExistingAvatars)
        {
            var existingAvatars = await _userFiles.GetActiveAvatarsByUserAsync(userId);
            foreach (var avatar in existingAvatars)
            {
                avatar.IsDeleted = true;
                await _userFiles.UpdateAsync(avatar);
                await _storage.DeleteAsync(avatar.UserId, avatar.StoredFileName, cancellationToken);
            }
        }

        await _storage.SaveAsync(userId, storedFileName, content, cancellationToken);

        var entity = new UserFile
        {
            UserId = userId,
            OriginalFileName = Path.GetFileName(originalFileName),
            StoredFileName = storedFileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Category = normalizedCategory,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            PaperId = paperId,
            CreatedAt = DateTime.UtcNow
        };

        await _userFiles.AddAsync(entity);
        return MapToDto(entity);
    }

    private static string? NormalizeCategory(string? category)
    {
        return string.IsNullOrWhiteSpace(category)
            ? null
            : category.Trim().ToLowerInvariant();
    }

    private static void ValidateCategory(string category)
    {
        if (!FileCategories.All.Contains(category))
        {
            throw new InvalidOperationException($"Category must be one of: {string.Join(", ", FileCategories.All)}.");
        }
    }

    private void ValidateContentType(string category, string contentType)
    {
        var allowed = category switch
        {
            FileCategories.Avatar or FileCategories.Image => _settings.AllowedImageTypes,
            _ => _settings.AllowedDocumentTypes
        };

        if (!allowed.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"File type '{contentType}' is not allowed for category '{category}'.");
        }
    }

    private void ValidateSize(string category, long sizeBytes)
    {
        var maxBytes = category switch
        {
            FileCategories.Avatar => _settings.MaxAvatarSizeMb * 1024L * 1024L,
            FileCategories.Image => _settings.MaxImageSizeMb * 1024L * 1024L,
            _ => _settings.MaxDocumentSizeMb * 1024L * 1024L
        };

        if (sizeBytes > maxBytes)
        {
            throw new InvalidOperationException("File exceeds the maximum allowed size.");
        }
    }

    private static string InferCategory(string contentType)
    {
        return contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            ? FileCategories.Image
            : FileCategories.Document;
    }

    private static string GetExtension(string originalFileName, string contentType)
    {
        var extension = Path.GetExtension(originalFileName);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension.ToLowerInvariant();
        }

        return ContentTypeExtensions.TryGetValue(contentType, out var mapped)
            ? mapped
            : throw new InvalidOperationException("Could not determine a safe file extension.");
    }

    private static FileUploadResultDto MapToDto(UserFile file)
    {
        return new FileUploadResultDto
        {
            Id = file.Id,
            FileName = file.OriginalFileName,
            ContentType = file.ContentType,
            SizeBytes = file.SizeBytes,
            Category = file.Category,
            Description = file.Description,
            PaperId = file.PaperId,
            Url = $"/api/files/{file.Id}/download",
            CreatedAt = file.CreatedAt
        };
    }
}
