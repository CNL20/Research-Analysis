using Microsoft.Extensions.Configuration;
using ScholarTrend.Application.DTOs.Sync;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Application.Interfaces.External;
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

        SyncResultDto? lastResult = null;
        foreach (var source in sources)
        {
            lastResult = await SyncSourceAsync(source);
        }

        return lastResult ?? new SyncResultDto { Status = "Skipped", Message = "No sources processed." };
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

    private async Task<SyncResultDto> SyncSourceAsync(ApiDataSource source)
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
            var externalPapers = source.Name switch
            {
                SemanticScholarName => await _semanticScholarClient.SearchPapersAsync(_defaultSearchQuery, 10),
                OpenAlexName => await _openAlexClient.SearchPapersAsync(_defaultSearchQuery, 10),
                _ => throw new InvalidOperationException($"Unsupported data source: {source.Name}")
            };

            log.PapersFetched = externalPapers.Count;

            var journals = await _unitOfWork.Journals.GetAllAsync();
            var defaultJournalId = journals.FirstOrDefault()?.Id;

            foreach (var external in externalPapers)
            {
                var result = await _paperImportRepository.ImportAsync(external, defaultJournalId);
                if (result.IsNew)
                {
                    log.PapersAdded++;
                    await _notificationService.NotifyFollowersForNewPaperAsync(result.PaperId);
                }
                else
                {
                    log.PapersUpdated++;
                }
            }

            source.LastSyncAt = DateTime.UtcNow;
            _unitOfWork.ApiDataSources.Update(source);

            log.Status = "Completed";
            log.CompletedAt = DateTime.UtcNow;
            _unitOfWork.SyncLogs.Update(log);
            await _unitOfWork.SaveChangesAsync();

            return new SyncResultDto
            {
                SyncLogId = log.Id,
                Source = source.Name,
                PapersFetched = log.PapersFetched,
                PapersAdded = log.PapersAdded,
                PapersUpdated = log.PapersUpdated,
                Status = log.Status,
                Message = "Sync completed successfully."
            };
        }
        catch (Exception ex)
        {
            log.Status = "Failed";
            log.ErrorMessage = ex.Message;
            log.CompletedAt = DateTime.UtcNow;
            _unitOfWork.SyncLogs.Update(log);
            await _unitOfWork.SaveChangesAsync();

            return new SyncResultDto
            {
                SyncLogId = log.Id,
                Source = source.Name,
                PapersFetched = log.PapersFetched,
                PapersAdded = log.PapersAdded,
                PapersUpdated = log.PapersUpdated,
                Status = log.Status,
                Message = ex.Message
            };
        }
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
