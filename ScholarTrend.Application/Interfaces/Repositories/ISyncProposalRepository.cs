using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface ISyncProposalRepository
{
    Task AddAsync(SyncProposal proposal);
    Task<SyncProposal?> GetByIdWithPapersAsync(int id);
    Task<IReadOnlyList<SyncProposal>> GetPendingProposalsAsync(int limit = 50);
    void Update(SyncProposal proposal);
    Task<bool> IsPaperAlreadyQueuedOrStoredAsync(string externalId, string externalSource);
}
