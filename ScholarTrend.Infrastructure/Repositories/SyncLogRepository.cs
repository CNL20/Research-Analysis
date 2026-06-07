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

    public async Task<IReadOnlyList<SyncLog>> GetRecentAsync(int limit = 50)
    {
        return await _context.SyncLogs
            .OrderByDescending(l => l.StartedAt)
            .Take(limit)
            .ToListAsync();
    }

    public void Update(SyncLog log)
    {
        _context.SyncLogs.Update(log);
    }
}
