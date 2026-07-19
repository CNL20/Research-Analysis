using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

public class AnalysisJobRepository : IAnalysisJobRepository
{
    private readonly DbSet<AnalysisJob> _db;

    public AnalysisJobRepository(ScholarTrendDbContext context)
    {
        _db = context.Set<AnalysisJob>();
    }

    public async Task<AnalysisJob?> GetByIdAsync(int id) => await _db.FindAsync(id);

    public async Task<AnalysisJob?> GetPendingByPaperIdAsync(int paperId)
    {
        return await _db.FirstOrDefaultAsync(x => 
            x.PaperId == paperId && 
            (x.Status == AnalysisJobStatus.Pending || x.Status == AnalysisJobStatus.Running));
    }

    public async Task<List<AnalysisJob>> GetPendingJobsAsync(int take = 50)
    {
        return await _db
            .Where(x => x.Status == AnalysisJobStatus.Pending)
            .OrderBy(x => x.StartedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<AnalysisJob>> GetByStatusAsync(string status, int take = 50)
    {
        return await _db
            .Where(x => x.Status == status)
            .OrderBy(x => x.StartedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task AddAsync(AnalysisJob job)
    {
        await _db.AddAsync(job);
    }

    public void Update(AnalysisJob job)
    {
        _db.Update(job);
    }
}
