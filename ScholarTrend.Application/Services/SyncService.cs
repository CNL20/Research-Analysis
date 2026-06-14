using System.Text.Json;
using Microsoft.Extensions.Configuration;
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

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaperImportRepository _paperImportRepository;
    private readonly ISemanticScholarClient _semanticScholarClient;
    private readonly IOpenAlexClient _openAlexClient;
    private readonly INotificationService _notificationService;
    private readonly string _defaultSearchQuery;

    public SyncService(
        IUnitOfWork unitOfWork,
        IPaperImportRepository paperImportRepository,
        ISemanticScholarClient semanticScholarClient,
        IOpenAlexClient openAlexClient,
        INotificationService notificationService,
        IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _paperImportRepository = paperImportRepository;
        _semanticScholarClient = semanticScholarClient;
        _openAlexClient = openAlexClient;
        _notificationService = notificationService;
        _defaultSearchQuery = configuration["ExternalApis:SemanticScholar:SearchQuery"] ?? "artificial intelligence";
    }

    public async Task<SyncResultDto> RunSyncAsync(string? sourceName = null)
    {
        var sources = await _unitOfWork.ApiDataSources.GetActiveAsync();
        if (!string.IsNullOrWhiteSpace(sourceName))
        {
            sources = sources.Where(s => s.Name.Equals(sourceName, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (sources.Count == 0)
        {
            throw new InvalidOperationException("No active API data sources found.");
        }

        var proposal = new SyncProposal
        {
            CreatedAt = DateTime.UtcNow,
            Status = SyncProposalStatus.Pending
        };

        await _unitOfWork.SyncProposals.AddAsync(proposal);
        await _unitOfWork.SaveChangesAsync();

        var log = new SyncLog
        {
            Source = string.Join(", ", sources.Select(s => s.Name)),
            Status = "Running",
            StartedAt = DateTime.UtcNow
        };

        await _unitOfWork.SyncLogs.AddAsync(log);
        await _unitOfWork.SaveChangesAsync();

        try
        {
            foreach (var source in sources)
            {
                var externalPapers = source.Name switch
                {
                    SemanticScholarName => await _semanticScholarClient.SearchPapersAsync(_defaultSearchQuery, 10),
                    OpenAlexName => await _openAlexClient.SearchPapersAsync(_defaultSearchQuery, 10),
                    _ => throw new InvalidOperationException($"Unsupported data source: {source.Name}")
                };

                log.PapersFetched += externalPapers.Count;

                foreach (var external in externalPapers)
                {
                    if (await _unitOfWork.SyncProposals.IsPaperAlreadyQueuedOrStoredAsync(external.ExternalId, external.Source))
                    {
                        continue;
                    }

                    proposal.PendingPapers.Add(MapToPendingPaper(external, proposal.Id));
                }

                source.LastSyncAt = DateTime.UtcNow;
                _unitOfWork.ApiDataSources.Update(source);
            }

            proposal.TotalFetched = proposal.PendingPapers.Count;
            _unitOfWork.SyncProposals.Update(proposal);

            log.Status = proposal.TotalFetched > 0 ? "AwaitingApproval" : "Completed";
            log.CompletedAt = DateTime.UtcNow;
            _unitOfWork.SyncLogs.Update(log);
            await _unitOfWork.SaveChangesAsync();

            if (proposal.TotalFetched > 0)
            {
                await _notificationService.NotifyAdminsPendingSyncAsync(proposal.Id, proposal.TotalFetched);
            }

            return new SyncResultDto
            {
                SyncProposalId = proposal.Id,
                SyncLogId = log.Id,
                Source = log.Source,
                PapersFetched = log.PapersFetched,
                PapersAdded = 0,
                PapersUpdated = 0,
                Status = log.Status,
                Message = proposal.TotalFetched > 0
                    ? $"{proposal.TotalFetched} paper(s) are awaiting admin approval."
                    : "No new papers found to sync."
            };
        }
        catch (Exception ex)
        {
            log.Status = "Failed";
            log.ErrorMessage = ex.Message;
            log.CompletedAt = DateTime.UtcNow;
            _unitOfWork.SyncLogs.Update(log);

            proposal.Status = SyncProposalStatus.Rejected;
            _unitOfWork.SyncProposals.Update(proposal);
            await _unitOfWork.SaveChangesAsync();

            return new SyncResultDto
            {
                SyncProposalId = proposal.Id,
                SyncLogId = log.Id,
                Source = log.Source,
                PapersFetched = log.PapersFetched,
                Status = log.Status,
                Message = ex.Message
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

        var rejectedCount = 0;
        foreach (var pending in proposal.PendingPapers.Where(p => p.Status == PendingPaperStatus.Pending))
        {
            pending.Status = PendingPaperStatus.Rejected;
            rejectedCount++;
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
            Message = $"{rejectedCount} pending paper(s) rejected."
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
}
