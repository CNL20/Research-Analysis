using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Persistence.Repositories;

public class PaperPdfFileRepository : IPaperPdfFileRepository
{
    private readonly ScholarTrendDbContext _context;
    private readonly DbSet<PaperPdfFile> _db;

    public PaperPdfFileRepository(ScholarTrendDbContext context)
    {
        _context = context;
        _db = context.Set<PaperPdfFile>();
    }

    public Task<PaperPdfFile?> GetByIdAsync(int id)
    {
        return _db.FirstOrDefaultAsync(p => p.Id == id);
    }

    public Task<PaperPdfFile?> GetByResearchPaperIdAsync(int researchPaperId)
    {
        return _db
            .FirstOrDefaultAsync(p => p.ResearchPaperId == researchPaperId);
    }

    public async Task<List<PaperPdfFile>> GetByStatusAsync(string status, int take = 100)
    {
        return await _db
            .Where(p => p.Status == status)
            .OrderBy(p => p.EnqueuedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<PaperPdfFile>> GetStuckAsync(IEnumerable<string> statuses, int take = 100)
    {
        var statusList = statuses.ToList();
        return await _db
            .Where(p => statusList.Contains(p.Status))
            .OrderBy(p => p.EnqueuedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task AddAsync(PaperPdfFile entity)
    {
        await _db.AddAsync(entity);
    }

    public void Update(PaperPdfFile entity)
    {
        _db.Update(entity);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
