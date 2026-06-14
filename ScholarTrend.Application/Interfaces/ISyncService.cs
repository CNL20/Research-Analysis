using ScholarTrend.Application.DTOs.Sync;

namespace ScholarTrend.Application.Interfaces;

public interface ISyncService
{
    /// <summary>
    /// Fetch papers from external APIs and create a pending sync proposal (requires admin approval).
    /// </summary>
    Task<SyncResultDto> RunSyncAsync(string? sourceName = null);

    Task<IReadOnlyList<SyncProposalListItemDto>> GetPendingProposalsAsync(int limit = 50);
    Task<SyncProposalDto> GetPendingProposalByIdAsync(int id);
    Task<ApproveSyncResultDto> ApprovePendingSyncAsync(int proposalId, string adminUserId, ApproveSyncRequest request);
    Task<ApproveSyncResultDto> RejectPendingSyncAsync(int proposalId, string adminUserId);

    Task<IReadOnlyList<SyncLogDto>> GetSyncLogsAsync(int limit = 50);
    Task<IReadOnlyList<ApiDataSourceDto>> GetDataSourcesAsync();
    Task<ApiDataSourceDto> UpdateDataSourceAsync(int id, UpdateApiDataSourceRequest request);
}
