using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface ISyncLogRepository
{
    Task AddAsync(SyncLog log);
    Task<SyncLog?> GetByIdAsync(int id);
    Task<IReadOnlyList<SyncLog>> GetRecentAsync(int limit = 50);
    void Update(SyncLog log);
}
