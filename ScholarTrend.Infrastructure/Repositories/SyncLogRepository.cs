using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

public class SyncLogRepository : ISyncLogRepository
{
    private readonly ScholarTrendDbContext _context;

    public SyncLogRepository(ScholarTrendDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SyncLog log)
    {
        await _context.SyncLogs.AddAsync(log);
    }

    public Task<SyncLog?> GetByIdAsync(int id)
    {
        return _context.SyncLogs.FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task<(IReadOnlyList<SyncLog> Items, int TotalCount)> GetRecentAsync(int page = 1, int pageSize = 20)
    {
        var query = _context.SyncLogs;
        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(l => l.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public void Update(SyncLog log)
    {
        _context.SyncLogs.Update(log);
    }
}
