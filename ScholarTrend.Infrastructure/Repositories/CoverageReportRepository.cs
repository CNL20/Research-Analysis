using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

public class CoverageReportRepository : ICoverageReportRepository
{
    private readonly DbSet<CoverageReport> _db;

    public CoverageReportRepository(ScholarTrendDbContext context)
    {
        _db = context.Set<CoverageReport>();
    }

    public async Task<CoverageReport?> GetLatestByTopicIdAsync(int topicId)
    {
        return await _db
            .Where(x => x.TopicId == topicId)
            .OrderByDescending(x => x.GeneratedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<CoverageReport?> GetByIdAsync(int id) => await _db.FindAsync(id);

    public async Task AddAsync(CoverageReport report)
    {
        await _db.AddAsync(report);
    }

    public async Task UpdateAsync(CoverageReport report)
    {
        _db.Update(report);
    }
}
