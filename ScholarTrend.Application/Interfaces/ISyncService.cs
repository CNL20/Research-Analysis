using ScholarTrend.Application.DTOs.Sync;

namespace ScholarTrend.Application.Interfaces;

public interface ISyncService
{
    Task<SyncResultDto> RunSyncAsync(string? sourceName = null);
    Task<IReadOnlyList<SyncLogDto>> GetSyncLogsAsync(int limit = 50);
    Task<IReadOnlyList<ApiDataSourceDto>> GetDataSourcesAsync();
    Task<ApiDataSourceDto> UpdateDataSourceAsync(int id, UpdateApiDataSourceRequest request);
}
