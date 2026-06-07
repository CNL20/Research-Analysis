using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

public class ApiDataSourceRepository : IApiDataSourceRepository
{
    private readonly ScholarTrendDbContext _context;

    public ApiDataSourceRepository(ScholarTrendDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ApiDataSource>> GetAllAsync()
    {
        return await _context.ApiDataSources.OrderBy(s => s.Name).ToListAsync();
    }

    public Task<ApiDataSource?> GetByIdAsync(int id)
    {
        return _context.ApiDataSources.FirstOrDefaultAsync(s => s.Id == id);
    }

    public Task<ApiDataSource?> GetByNameAsync(string name)
    {
        return _context.ApiDataSources.FirstOrDefaultAsync(s => s.Name == name);
    }

    public async Task<IReadOnlyList<ApiDataSource>> GetActiveAsync()
    {
        return await _context.ApiDataSources.Where(s => s.IsActive).ToListAsync();
    }

    public void Update(ApiDataSource source)
    {
        _context.ApiDataSources.Update(source);
    }
}
