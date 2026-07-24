using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

public class AuthorRepository : GenericRepository<Author>, IAuthorRepository
{
    public AuthorRepository(ScholarTrendDbContext context) : base(context)
    {
    }

    public Task<Author?> GetByNameAsync(string name)
    {
        var normalizedName = name.Trim().ToLower();
        return _dbSet.FirstOrDefaultAsync(a => a.Name.ToLower() == normalizedName);
    }

    public async Task<(IReadOnlyList<Author> Items, int TotalCount)> GetPagedAsync(string? keyword, int page, int pageSize)
    {
        var query = _dbSet.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lowerKeyword = keyword.Trim().ToLower();
            query = query.Where(a => 
                a.Name.ToLower().Contains(lowerKeyword) || 
                (a.Affiliation != null && a.Affiliation.ToLower().Contains(lowerKeyword)) || 
                (a.Country != null && a.Country.ToLower().Contains(lowerKeyword)));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}
