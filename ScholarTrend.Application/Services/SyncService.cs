using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.Sync;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Constants;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Services;

public class SyncService : ISyncService
{
    private const string SemanticScholarName = "SemanticScholar";
    private const string OpenAlexName = "OpenAlex";
    private const string CrossrefName = "Crossref";
    private const string ArXivName = "ArXiv";
    private const string SyncTypeManual = "Manual";
    private const string SyncTypeAutomatic = "Automatic";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaperImportRepository _paperImportRepository;
    private readonly ISemanticScholarClient _semanticScholarClient;
    private readonly IOpenAlexClient _openAlexClient;
    private readonly ICrossrefClient _crossrefClient;
    private readonly IArXivClient _arXivClient;
    private readonly INotificationService _notificationService;
    private readonly ILogger<SyncService> _logger;
    private readonly string _defaultSearchQuery;
    private readonly IReadOnlyList<string> _defaultSearchQueries;
    private readonly int _semanticScholarPageSize;
    private readonly int _openAlexPageSize;

    public SyncService(
        IUnitOfWork unitOfWork,
        IPaperImportRepository paperImportRepository,
        ISemanticScholarClient semanticScholarClient,
        IOpenAlexClient openAlexClient,
        ICrossrefClient crossrefClient,
        IArXivClient arXivClient,
        INotificationService notificationService,
        IConfiguration configuration,
        ILogger<SyncService> logger)
    {
        _unitOfWork = unitOfWork;
        _paperImportRepository = paperImportRepository;
        _semanticScholarClient = semanticScholarClient;
        _openAlexClient = openAlexClient;
        _crossrefClient = crossrefClient;
        _arXivClient = arXivClient;
        _notificationService = notificationService;
        _logger = logger;
        _defaultSearchQuery = configuration["ExternalApis:SemanticScholar:SearchQuery"] ?? "artificial intelligence";
        _defaultSearchQueries = ReadDefaultSearchQueries(configuration);
        _semanticScholarPageSize = int.TryParse(configuration["ExternalApis:SemanticScholar:PageSize"], out var ss) ? ss : 10;
        _openAlexPageSize = int.TryParse(configuration["ExternalApis:OpenAlex:PageSize"], out var oa) ? oa : 10;
    }

    private static IReadOnlyList<string> ReadDefaultSearchQueries(IConfiguration configuration)
    {
        var section = configuration.GetSection("SyncSchedule:SearchQueries");
        var values = new List<string>();
        foreach (var child in section.GetChildren())
        {
            var v = child.Value;
            if (!string.IsNullOrWhiteSpace(v))
            {
                values.Add(v);
            }
        }

        var cleaned = values
            .Where(q => !string.IsNullOrWhiteSpace(q))
            .Select(q => q.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return cleaned.Count > 0 ? cleaned : new List<string> { configuration["ExternalApis:SemanticScholar:SearchQuery"] ?? "artificial intelligence" };
    }

    public DbContext Context => _unitOfWork.Context;

    public async Task<MultiSyncResultDto> RunSyncAsync(
        string? sourceName = null,
        string syncType = "Manual",
        string? triggeredBy = null,
        List<string>? searchQueries = null,
        int? paperLimit = null)
    {
        _logger.LogInformation("Starting {SyncType} sync triggered by {TriggeredBy} for source: {Source} (queries: {QueryCount}, limit: {Limit})",
            syncType, triggeredBy ?? "system", sourceName ?? "all",
            searchQueries?.Count ?? 1, paperLimit?.ToString() ?? "default");

        var sources = await _unitOfWork.ApiDataSources.GetActiveAsync();
        if (!string.IsNullOrWhiteSpace(sourceName))
        {
            sources = sources.Where(s => s.Name.Equals(sourceName, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (sources.Count == 0)
        {
            throw new InvalidOperationException("No active API data sources found.");
        }

        // Normalize queries: empty/null => fall back to the full default query list from SyncSchedule:SearchQueries.
        var normalizedQueries = (searchQueries == null || searchQueries.Count == 0)
            ? _defaultSearchQueries.ToList()
            : searchQueries
                .Where(q => !string.IsNullOrWhiteSpace(q))
                .Select(q => q.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        if (normalizedQueries.Count == 0)
        {
            normalizedQueries = _defaultSearchQueries.ToList();
        }

        var results = new List<SyncResultDto>();

        foreach (var source in sources)
        {
            var lockKey = $"{syncType}:{source.Name}";

            if (!SyncLockManager.TryAcquireLock(source.Name, syncType, triggeredBy ?? "system", out var lockInfo))
            {
                var existingLock = SyncLockManager.GetLockStatus(source.Name);
                _logger.LogWarning("Sync for {Source} is already running (Type: {Type}, By: {By})",
                    source.Name, existingLock?.SyncType, existingLock?.TriggeredBy);

                results.Add(new SyncResultDto
                {
                    Source = source.Name,
                    Status = "Skipped",
                    Message = $"Sync is already in progress. Started by {existingLock?.SyncType} at {existingLock?.AcquiredAt:g}."
                });
                continue;
            }

            try
            {
                var sourceResults = await SyncSingleSourceAsync(source, syncType, triggeredBy, normalizedQueries, paperLimit);
                results.AddRange(sourceResults);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync failed for source {Source} (queries={QueryCount})", source.Name, normalizedQueries.Count);

                // Add a single "Failed" result so callers see the error and continue with remaining sources.
                results.Add(new SyncResultDto
                {
                    Source = source.Name,
                    Query = string.Join(", ", normalizedQueries),
                    PapersFetched = 0,
                    PapersAdded = 0,
                    PapersUpdated = 0,
                    Status = "Failed",
                    Message = $"Source-level sync failed: {ex.Message}"
                });
            }
            finally
            {
                SyncLockManager.ReleaseLock(source.Name);
            }
        }

        var totalFetched = results.Sum(r => r.PapersFetched);
        var totalQueued = results.Sum(r => r.PapersAdded);
        var failedCount = results.Count(r => r.Status == "Failed");
        var skippedCount = results.Count(r => r.Status == "Skipped");

        _logger.LogInformation(
            "{SyncType} sync completed: TotalFetched={Fetched}, TotalQueued={Queued}, Failed={Failed}, Skipped={Skipped}, Proposals={Proposals}",
            syncType, totalFetched, totalQueued, failedCount, skippedCount, results.Count(r => r.SyncProposalId.HasValue));

        return new MultiSyncResultDto
        {
            Results = results,
            SyncType = syncType,
            TriggeredBy = triggeredBy ?? "system",
            StartedAt = DateTime.UtcNow,
            TotalFetched = totalFetched,
            TotalQueued = totalQueued
        };
    }

    private async Task<List<SyncResultDto>> SyncSingleSourceAsync(
        ApiDataSource source,
        string syncType,
        string? triggeredBy,
        List<string> searchQueries,
        int? paperLimit)
    {
        var results = new List<SyncResultDto>();

        foreach (var (query, index) in searchQueries.Select((q, i) => (q, i)))
        {
            var single = await SyncSingleQueryAsync(source, syncType, triggeredBy, query, paperLimit, index + 1, searchQueries.Count);
            results.Add(single);
        }

        source.LastSyncAt = DateTime.UtcNow;
        _unitOfWork.ApiDataSources.Update(source);
        await _unitOfWork.SaveChangesAsync();

        return results;
    }

    /// <summary>
    /// Sync one source against one query, creating one SyncProposal for the resulting papers.
    /// </summary>
    private async Task<SyncResultDto> SyncSingleQueryAsync(
        ApiDataSource source,
        string syncType,
        string? triggeredBy,
        string searchQuery,
        int? paperLimit,
        int queryIndex,
        int totalQueries)
    {
        var log = new SyncLog
        {
            Source = source.Name,
            Status = "Running",
            StartedAt = DateTime.UtcNow
        };

        await _unitOfWork.SyncLogs.AddAsync(log);
        await _unitOfWork.SaveChangesAsync();

        try
        {
            var limit = paperLimit ?? (source.Name == OpenAlexName ? _openAlexPageSize : _semanticScholarPageSize);

            IReadOnlyList<ExternalPaperDto> externalPapers;

            try
            {
                externalPapers = source.Name switch
                {
                    SemanticScholarName => await _semanticScholarClient.SearchPapersAsync(searchQuery, limit),
                    OpenAlexName => await _openAlexClient.SearchPapersAsync(searchQuery, limit),
                    CrossrefName => await _crossrefClient.SearchPapersAsync(searchQuery, limit),
                    ArXivName => await _arXivClient.SearchPapersAsync(searchQuery, limit),
                    _ => throw new InvalidOperationException($"Unsupported data source: {source.Name}")
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch papers from {Source} for query '{Query}', continuing with empty list", source.Name, searchQuery);
                externalPapers = [];
            }

            log.PapersFetched = externalPapers.Count;

            var journals = await _unitOfWork.Journals.GetAllAsync();
            var defaultJournalId = journals.FirstOrDefault()?.Id;

            if (defaultJournalId == null)
            {
                throw new InvalidOperationException("No journals found in the system. Please seed journals before syncing.");
            }

            var newPapersToAdd = new List<ExternalPaperDto>();

            foreach (var external in externalPapers)
            {
                if (await _unitOfWork.SyncProposals.IsPaperAlreadyQueuedOrStoredAsync(external.ExternalId, external.Source))
                {
                    continue;
                }

                newPapersToAdd.Add(external);
            }

            if (newPapersToAdd.Count == 0)
            {
                log.Status = "Completed";
                log.CompletedAt = DateTime.UtcNow;
                _unitOfWork.SyncLogs.Update(log);
                await RetrySaveChangesAsync();

                return new SyncResultDto
                {
                    SyncProposalId = null,
                    SyncLogId = log.Id,
                    Source = source.Name,
                    Query = searchQuery,
                    PapersFetched = log.PapersFetched,
                    PapersAdded = 0,
                    PapersUpdated = 0,
                    Status = log.Status,
                    Message = $"[{searchQuery}] No new papers found to sync."
                };
            }

            var proposal = new SyncProposal
            {
                CreatedAt = DateTime.UtcNow,
                Status = SyncProposalStatus.Pending
            };

            await _unitOfWork.SyncProposals.AddAsync(proposal);
            await _unitOfWork.SaveChangesAsync();

            foreach (var external in newPapersToAdd)
            {
                proposal.PendingPapers.Add(MapToPendingPaper(external, proposal.Id));
            }

            proposal.TotalFetched = proposal.PendingPapers.Count;
            _unitOfWork.SyncProposals.Update(proposal);

            log.Status = "Completed";
            log.CompletedAt = DateTime.UtcNow;
            log.PapersAdded = newPapersToAdd.Count;
            _unitOfWork.SyncLogs.Update(log);
            await RetrySaveChangesAsync();

            await _notificationService.NotifyAdminsPendingSyncAsync(proposal.Id, proposal.TotalFetched);

            return new SyncResultDto
            {
                SyncProposalId = proposal.Id,
                SyncLogId = log.Id,
                Source = source.Name,
                Query = searchQuery,
                PapersFetched = log.PapersFetched,
                PapersAdded = newPapersToAdd.Count,
                PapersUpdated = 0,
                Status = log.Status,
                Message = $"[{searchQuery}] {proposal.TotalFetched} paper(s) are awaiting admin approval."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed for source {Source} query '{Query}'", source.Name, searchQuery);

            log.Status = "Failed";
            log.ErrorMessage = ex.Message;
            log.CompletedAt = DateTime.UtcNow;
            _unitOfWork.SyncLogs.Update(log);

            try
            {
                await _unitOfWork.SaveChangesAsync();
            }
            catch
            {
                // Ignore save errors for failed sync
            }

            return new SyncResultDto
            {
                SyncProposalId = null,
                SyncLogId = log.Id,
                Source = source.Name,
                Query = searchQuery,
                PapersFetched = log.PapersFetched,
                PapersAdded = 0,
                PapersUpdated = 0,
                Status = log.Status,
                Message = $"[{searchQuery}] Sync failed: {ex.Message}"
            };
        }
    }

    public async Task<IReadOnlyList<SyncProposalListItemDto>> GetPendingProposalsAsync(int limit = 50)
    {
        var proposals = await _unitOfWork.SyncProposals.GetPendingProposalsAsync(limit);
        return proposals.Select(MapProposalToListItem).ToList();
    }

    public async Task<SyncProposalDto> GetPendingProposalByIdAsync(int id)
    {
        var proposal = await _unitOfWork.SyncProposals.GetByIdWithPapersAsync(id);
        if (proposal == null)
        {
            throw new InvalidOperationException("Sync proposal not found.");
        }

        return MapProposalToDto(proposal);
    }

    public async Task<ApproveSyncResultDto> ApprovePendingSyncAsync(int proposalId, string adminUserId, ApproveSyncRequest request)
    {
        var proposal = await _unitOfWork.SyncProposals.GetByIdWithPapersAsync(proposalId);
        if (proposal == null)
        {
            throw new InvalidOperationException("Sync proposal not found.");
        }

        if (proposal.Status is SyncProposalStatus.Approved or SyncProposalStatus.Rejected)
        {
            throw new InvalidOperationException("This sync proposal has already been reviewed.");
        }

        var pendingPapers = proposal.PendingPapers
            .Where(p => p.Status == PendingPaperStatus.Pending)
            .ToList();

        if (request.PendingPaperIds is { Count: > 0 })
        {
            var selectedIds = request.PendingPaperIds.ToHashSet();
            pendingPapers = pendingPapers.Where(p => selectedIds.Contains(p.Id)).ToList();
        }

        if (pendingPapers.Count == 0)
        {
            throw new InvalidOperationException("No pending papers available to approve.");
        }

        var journals = await _unitOfWork.Journals.GetAllAsync();
        var defaultJournalId = journals.FirstOrDefault()?.Id;
        var approvedCount = 0;

        foreach (var pending in pendingPapers)
        {
            var external = MapToExternalPaper(pending);
            var result = await _paperImportRepository.ImportAsync(external, defaultJournalId);

            pending.Status = PendingPaperStatus.Approved;
            pending.ImportedPaperId = result.PaperId;
            approvedCount++;

            await _notificationService.NotifyFollowersForNewPaperAsync(result.PaperId);
        }

        proposal.TotalApproved += approvedCount;
        proposal.ReviewedByUserId = adminUserId;
        proposal.ReviewedAt = DateTime.UtcNow;

        var remainingPending = proposal.PendingPapers.Count(p => p.Status == PendingPaperStatus.Pending);
        proposal.Status = remainingPending > 0
            ? SyncProposalStatus.PartiallyApproved
            : SyncProposalStatus.Approved;

        _unitOfWork.SyncProposals.Update(proposal);
        await _unitOfWork.SaveChangesAsync();

        return new ApproveSyncResultDto
        {
            SyncProposalId = proposal.Id,
            Status = proposal.Status,
            PapersApproved = approvedCount,
            PapersRejected = 0,
            Message = $"{approvedCount} paper(s) approved and imported successfully."
        };
    }

    public async Task<ApproveSyncResultDto> RejectPendingSyncAsync(int proposalId, string adminUserId)
    {
        var proposal = await _unitOfWork.SyncProposals.GetByIdWithPapersAsync(proposalId);
        if (proposal == null)
        {
            throw new InvalidOperationException("Sync proposal not found.");
        }

        if (proposal.Status is SyncProposalStatus.Approved or SyncProposalStatus.Rejected)
        {
            throw new InvalidOperationException("This sync proposal has already been reviewed.");
        }

        var pendingPapers = proposal.PendingPapers.Where(p => p.Status == PendingPaperStatus.Pending).ToList();
        var rejectedCount = pendingPapers.Count;

        // Delete rejected pending papers (per workflow diagram)
        // Remove from the collection - EF Core will delete when SaveChanges is called
        foreach (var paper in pendingPapers)
        {
            proposal.PendingPapers.Remove(paper);
        }

        proposal.Status = SyncProposalStatus.Rejected;
        proposal.ReviewedByUserId = adminUserId;
        proposal.ReviewedAt = DateTime.UtcNow;

        _unitOfWork.SyncProposals.Update(proposal);
        await _unitOfWork.SaveChangesAsync();

        return new ApproveSyncResultDto
        {
            SyncProposalId = proposal.Id,
            Status = proposal.Status,
            PapersApproved = 0,
            PapersRejected = rejectedCount,
            Message = $"{rejectedCount} pending paper(s) rejected and deleted."
        };
    }

    public async Task<IReadOnlyList<SyncLogDto>> GetSyncLogsAsync(int limit = 50)
    {
        var logs = await _unitOfWork.SyncLogs.GetRecentAsync(limit);
        return logs.Select(MapLogToDto).ToList();
    }

    public async Task<IReadOnlyList<ApiDataSourceDto>> GetDataSourcesAsync()
    {
        var sources = await _unitOfWork.ApiDataSources.GetAllAsync();
        return sources.Select(MapSourceToDto).ToList();
    }

    public async Task<ApiDataSourceDto> UpdateDataSourceAsync(int id, UpdateApiDataSourceRequest request)
    {
        var source = await _unitOfWork.ApiDataSources.GetByIdAsync(id);
        if (source == null)
        {
            throw new InvalidOperationException("API data source not found.");
        }

        source.IsActive = request.IsActive;
        _unitOfWork.ApiDataSources.Update(source);
        await _unitOfWork.SaveChangesAsync();

        return MapSourceToDto(source);
    }

    private static PendingPaper MapToPendingPaper(ExternalPaperDto external, int proposalId)
    {
        return new PendingPaper
        {
            SyncProposalId = proposalId,
            ExternalId = external.ExternalId,
            ExternalSource = external.Source,
            Title = external.Title,
            Abstract = external.Abstract,
            Year = external.Year,
            CitationCount = external.CitationCount,
            Doi = external.Doi,
            Url = external.Url,
            AuthorNamesJson = JsonSerializer.Serialize(external.AuthorNames),
            Status = PendingPaperStatus.Pending
        };
    }

    private static ExternalPaperDto MapToExternalPaper(PendingPaper pending)
    {
        var authors = JsonSerializer.Deserialize<List<string>>(pending.AuthorNamesJson) ?? [];

        return new ExternalPaperDto
        {
            ExternalId = pending.ExternalId,
            Source = pending.ExternalSource,
            Title = pending.Title,
            Abstract = pending.Abstract,
            Year = pending.Year,
            CitationCount = pending.CitationCount,
            Doi = pending.Doi,
            Url = pending.Url,
            AuthorNames = authors
        };
    }

    private static SyncProposalListItemDto MapProposalToListItem(SyncProposal proposal)
    {
        return new SyncProposalListItemDto
        {
            Id = proposal.Id,
            CreatedAt = proposal.CreatedAt,
            Status = proposal.Status,
            TotalFetched = proposal.TotalFetched,
            PendingCount = proposal.PendingPapers.Count(p => p.Status == PendingPaperStatus.Pending),
            TotalApproved = proposal.TotalApproved
        };
    }

    private static SyncProposalDto MapProposalToDto(SyncProposal proposal)
    {
        return new SyncProposalDto
        {
            Id = proposal.Id,
            CreatedAt = proposal.CreatedAt,
            Status = proposal.Status,
            TotalFetched = proposal.TotalFetched,
            TotalApproved = proposal.TotalApproved,
            ReviewedByUserId = proposal.ReviewedByUserId,
            ReviewedAt = proposal.ReviewedAt,
            Papers = proposal.PendingPapers
                .OrderBy(p => p.Id)
                .Select(MapPendingPaperToDto)
                .ToList()
        };
    }

    private static PendingPaperDto MapPendingPaperToDto(PendingPaper pending)
    {
        var authors = JsonSerializer.Deserialize<List<string>>(pending.AuthorNamesJson) ?? [];

        return new PendingPaperDto
        {
            Id = pending.Id,
            ExternalId = pending.ExternalId,
            ExternalSource = pending.ExternalSource,
            Title = pending.Title,
            Abstract = pending.Abstract,
            Year = pending.Year,
            CitationCount = pending.CitationCount,
            Doi = pending.Doi,
            Url = pending.Url,
            Authors = authors,
            Status = pending.Status,
            ImportedPaperId = pending.ImportedPaperId
        };
    }

    private static SyncLogDto MapLogToDto(SyncLog log)
    {
        return new SyncLogDto
        {
            Id = log.Id,
            Source = log.Source,
            PapersFetched = log.PapersFetched,
            PapersAdded = log.PapersAdded,
            PapersUpdated = log.PapersUpdated,
            Status = log.Status,
            ErrorMessage = log.ErrorMessage,
            StartedAt = log.StartedAt,
            CompletedAt = log.CompletedAt
        };
    }

    private static ApiDataSourceDto MapSourceToDto(ApiDataSource source)
    {
        return new ApiDataSourceDto
        {
            Id = source.Id,
            Name = source.Name,
            BaseUrl = source.BaseUrl,
            IsActive = source.IsActive,
            LastSyncAt = source.LastSyncAt
        };
    }

    private async Task RetrySaveChangesAsync(int maxRetries = 3)
    {
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await _unitOfWork.SaveChangesAsync();
                return;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict on SaveChanges (attempt {Attempt}/{MaxRetries})", attempt, maxRetries);

                if (attempt == maxRetries)
                {
                    throw;
                }

                foreach (var entry in _unitOfWork.Context.ChangeTracker.Entries().ToList())
                {
                    entry.State = EntityState.Detached;
                }

                await Task.Delay(100 * attempt);
            }
        }
    }

    public bool IsSyncRunning(string sourceName)
    {
        return SyncLockManager.GetLockStatus(sourceName) != null;
    }

    public SyncLockStatusDto? GetSyncLockStatus(string sourceName)
    {
        var lockInfo = SyncLockManager.GetLockStatus(sourceName);
        if (lockInfo == null)
        {
            return new SyncLockStatusDto
            {
                SourceName = sourceName,
                IsLocked = false
            };
        }

        return new SyncLockStatusDto
        {
            SourceName = sourceName,
            IsLocked = true,
            SyncType = lockInfo.SyncType,
            TriggeredBy = lockInfo.TriggeredBy,
            LockedAt = lockInfo.AcquiredAt,
            ExpiresAt = lockInfo.ExpiresAt
        };
    }
}
