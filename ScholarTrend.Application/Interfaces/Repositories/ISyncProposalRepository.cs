using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface ISyncProposalRepository
{
    Task AddAsync(SyncProposal proposal);
    Task<SyncProposal?> GetByIdWithPapersAsync(int id);
    Task<(IReadOnlyList<SyncProposal> Items, int TotalCount)> GetPendingProposalsAsync(int page = 1, int pageSize = 20);
    void Update(SyncProposal proposal);
    Task<bool> IsPaperAlreadyQueuedOrStoredAsync(string externalId, string externalSource);

    Task<HashSet<string>> GetExistingExternalIdsAsync(IReadOnlyList<string> externalIds, string externalSource);
}
