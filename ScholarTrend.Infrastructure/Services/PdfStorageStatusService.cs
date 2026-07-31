using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.Migration;
using ScholarTrend.Domain.Constants;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation: query DB thẳng qua DbContext (không qua repository).
/// Application layer không có reference tới EF Core, nên query cần ở Infrastructure.
/// </summary>
public class PdfStorageStatusService
{
    private readonly ScholarTrendDbContext _db;
    private readonly ILogger<PdfStorageStatusService> _logger;

    public PdfStorageStatusService(ScholarTrendDbContext db, ILogger<PdfStorageStatusService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<PdfStorageStatusDto>> GetRecentAsync(int limit = 100, CancellationToken ct = default)
    {
        var (items, _) = await GetPagedAsync(page: 1, pageSize: limit, search: null, status: null, ct);
        return items;
    }

    public async Task<(List<PdfStorageStatusDto> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _db.PaperPdfFiles.AsNoTracking().AsQueryable();

        // Ready = PDF sẵn sàng nhưng chưa extract text
        // Extracted = đã có ExtractedText
        // Failed = Failed + Skipped
        // Pending = Queued / Downloading / ...
        var statusKey = (status ?? "all").Trim().ToLowerInvariant();
        query = statusKey switch
        {
            "ready" => query.Where(p =>
                p.Status == PaperDownloadStatus.Ready &&
                (p.ExtractedText == null || p.ExtractedText == "")),
            "extracted" => query.Where(p =>
                p.ExtractedText != null && p.ExtractedText != ""),
            "failed" => query.Where(p =>
                p.Status == PaperDownloadStatus.Failed ||
                p.Status == PaperDownloadStatus.Skipped),
            "pending" => query.Where(p =>
                p.Status != PaperDownloadStatus.Ready &&
                p.Status != PaperDownloadStatus.Failed &&
                p.Status != PaperDownloadStatus.Skipped),
            _ => query
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            if (int.TryParse(term.TrimStart('#'), out var paperId))
            {
                query = query.Where(p =>
                    p.ResearchPaperId == paperId ||
                    (p.ResearchPaper != null && p.ResearchPaper.Title.Contains(term)));
            }
            else
            {
                query = query.Where(p =>
                    p.ResearchPaper != null && p.ResearchPaper.Title.Contains(term));
            }
        }

        var totalCount = await query.CountAsync(ct);

        var pageRows = await query
            .OrderByDescending(p => p.EnqueuedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.ResearchPaperId,
                p.LocalRelativePath,
                p.Status,
                p.ContentType,
                p.SizeBytes,
                p.Sha256,
                p.ExternalSource,
                p.SourceUrl,
                p.AttemptCount,
                p.EnqueuedAt,
                p.CompletedAt,
                p.FailureReason,
                TextExtracted = p.ExtractedText != null && p.ExtractedText != ""
            })
            .ToListAsync(ct);

        var paperIds = pageRows.Select(i => i.ResearchPaperId).Distinct().ToList();
        var titles = await _db.ResearchPapers
            .AsNoTracking()
            .Where(p => paperIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Title })
            .ToListAsync(ct);
        var titleMap = titles.ToDictionary(p => p.Id, p => p.Title);

        var items = pageRows.Select(p => new PdfStorageStatusDto
        {
            PaperPdfFileId = p.Id,
            ResearchPaperId = p.ResearchPaperId,
            PaperTitle = titleMap.TryGetValue(p.ResearchPaperId, out var t) ? t : "(unknown)",
            LocalRelativePath = p.LocalRelativePath,
            Status = p.Status,
            ContentType = p.ContentType ?? "application/pdf",
            SizeBytes = p.SizeBytes,
            Sha256 = p.Sha256,
            ExternalSource = p.ExternalSource,
            SourceUrl = p.SourceUrl,
            AttemptCount = p.AttemptCount,
            EnqueuedAt = p.EnqueuedAt,
            CompletedAt = p.CompletedAt,
            FailureReason = p.FailureReason,
            TextExtracted = p.TextExtracted
        }).ToList();

        return (items, totalCount);
    }

    public async Task<Dictionary<string, int>> GetStatusSummaryAsync(CancellationToken ct = default)
    {
        var grouped = await _db.PaperPdfFiles
            .GroupBy(p => p.Status)
            .Select(g => new StatusCount { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var dict = grouped.ToDictionary(x => x.Status, x => x.Count);

        var extracted = await _db.PaperPdfFiles
            .CountAsync(p => p.ExtractedText != null && p.ExtractedText != "", ct);
        dict["ExtractedText"] = extracted;

        return dict;
    }

    private class StatusCount
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
