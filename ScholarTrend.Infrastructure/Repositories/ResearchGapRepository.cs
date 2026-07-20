using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

public class ResearchGapRepository : IResearchGapRepository
{
    private readonly DbSet<ResearchGap> _db;
    private readonly ScholarTrendDbContext _context;

    public ResearchGapRepository(ScholarTrendDbContext context)
    {
        _context = context;
        _db = context.Set<ResearchGap>();
    }

    public async Task<ResearchGap?> GetByIdAsync(int id) => await _db.FindAsync(id);

    public async Task<List<ResearchGap>> GetByTopicIdAsync(int topicId)
    {
        return await _db
            .Include(x => x.Evidences)
            .Where(x => x.TopicId == topicId)
            .OrderByDescending(x => x.Confidence)
            .ToListAsync();
    }

    public async Task<ResearchGap?> GetByIdWithEvidencesAsync(int id)
    {
        return await _db
            .Include(x => x.Evidences)
            .ThenInclude(e => e.Paper)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task AddAsync(ResearchGap gap)
    {
        await _db.AddAsync(gap);
    }

    public async Task AddEvidencesAsync(List<ResearchGapEvidence> evidences)
    {
        await _context.AddRangeAsync(evidences);
    }

    public async Task UpdateAsync(ResearchGap gap)
    {
        _db.Update(gap);
    }

    public async Task DeleteByTopicAsync(int topicId)
    {
        await _db.Where(x => x.TopicId == topicId).ExecuteDeleteAsync();
    }
}
