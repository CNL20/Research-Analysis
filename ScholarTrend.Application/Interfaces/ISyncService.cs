using ScholarTrend.Application.DTOs.Sync;

namespace ScholarTrend.Application.Interfaces;

public interface ISyncService
{
    /// <summary>
    /// Fetch papers from external APIs and create a pending sync proposal (requires admin approval).
    /// </summary>
    /// <param name="sourceName">Specific source to sync, or null for all active sources</param>
    /// <param name="syncType">Type of sync: "Manual" or "Automatic"</param>
    /// <param name="triggeredBy">User ID or system identifier for who/what triggered the sync</param>
    Task<MultiSyncResultDto> RunSyncAsync(string? sourceName = null, string syncType = "Manual", string? triggeredBy = null);

    Task<IReadOnlyList<SyncProposalListItemDto>> GetPendingProposalsAsync(int limit = 50);
    Task<SyncProposalDto> GetPendingProposalByIdAsync(int id);
    Task<ApproveSyncResultDto> ApprovePendingSyncAsync(int proposalId, string adminUserId, ApproveSyncRequest request);
    Task<ApproveSyncResultDto> RejectPendingSyncAsync(int proposalId, string adminUserId);

    Task<IReadOnlyList<SyncLogDto>> GetSyncLogsAsync(int limit = 50);
    Task<IReadOnlyList<ApiDataSourceDto>> GetDataSourcesAsync();
    Task<ApiDataSourceDto> UpdateDataSourceAsync(int id, UpdateApiDataSourceRequest request);
    
    /// <summary>
    /// Check if a sync is currently running for the specified source.
    /// </summary>
    bool IsSyncRunning(string sourceName);
    
    /// <summary>
    /// Get current lock status for a specific source.
    /// </summary>
    SyncLockStatusDto? GetSyncLockStatus(string sourceName);
}
