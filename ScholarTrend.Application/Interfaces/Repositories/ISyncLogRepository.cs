using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface ISyncLogRepository
{
    Task AddAsync(SyncLog log);
    Task<SyncLog?> GetByIdAsync(int id);
    Task<(IReadOnlyList<SyncLog> Items, int TotalCount)> GetRecentAsync(int page = 1, int pageSize = 20);
    void Update(SyncLog log);
}
