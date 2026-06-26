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
}
