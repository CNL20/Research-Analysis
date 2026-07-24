using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.Migration;
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
        var items = await _db.PaperPdfFiles
            .OrderByDescending(p => p.EnqueuedAt)
            .Take(limit)
            .Select(p => new PdfStorageStatusDto
            {
                PaperPdfFileId = p.Id,
                ResearchPaperId = p.ResearchPaperId,
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
                FailureReason = p.FailureReason
            })
            .ToListAsync(ct);

        // Tối ưu: load paper titles 1 lần
        var paperIds = items.Select(i => i.ResearchPaperId).Distinct().ToList();
        var titles = await _db.ResearchPapers
            .Where(p => paperIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Title })
            .ToListAsync(ct);

        var titleMap = titles.ToDictionary(p => p.Id, p => p.Title);
        foreach (var item in items)
        {
            item.PaperTitle = titleMap.TryGetValue(item.ResearchPaperId, out var t) ? t : "(unknown)";
        }

        return items;
    }

    public async Task<Dictionary<string, int>> GetStatusSummaryAsync(CancellationToken ct = default)
    {
        var grouped = await _db.PaperPdfFiles
            .GroupBy(p => p.Status)
            .Select(g => new StatusCount { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return grouped.ToDictionary(x => x.Status, x => x.Count);
    }

    private class StatusCount
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}