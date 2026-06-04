using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

public class JournalRepository : GenericRepository<Journal>, IJournalRepository
{
    public JournalRepository(ScholarTrendDbContext context) : base(context)
    {
    }

    public async Task<Journal?> GetByIssnAsync(string issn)
    {
        return await _dbSet.FirstOrDefaultAsync(j => j.Issn == issn);
    }
}
