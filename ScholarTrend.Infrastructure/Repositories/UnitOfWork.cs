using ScholarTrend.Application.Interfaces;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

/// <summary>
/// Unit of Work implementation coordinating SaveChanges across repositories.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly ScholarTrendDbContext _context;

    public UnitOfWork(ScholarTrendDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
