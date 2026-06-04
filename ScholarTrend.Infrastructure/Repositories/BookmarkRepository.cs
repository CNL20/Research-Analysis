using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

public class BookmarkRepository : GenericRepository<Bookmark>, IBookmarkRepository
{
    public BookmarkRepository(ScholarTrendDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Bookmark>> GetUserBookmarksAsync(string userId)
    {
        return await _dbSet
            .Include(b => b.Paper)
            .Where(b => b.UserId == userId)
            .ToListAsync();
    }

    public async Task<Bookmark?> GetBookmarkAsync(string userId, int paperId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(b => b.UserId == userId && b.PaperId == paperId);
    }
}
