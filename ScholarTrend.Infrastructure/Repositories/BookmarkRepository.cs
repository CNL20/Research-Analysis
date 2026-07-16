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

    public async Task<(IEnumerable<Bookmark> Items, int TotalCount)> GetUserBookmarksAsync(string userId, int page = 1, int pageSize = 10)
    {
        var query = _dbSet.Where(b => b.UserId == userId);
        var totalCount = await query.CountAsync();

        var items = await query
            .Include(b => b.Paper).ThenInclude(p => p.Journal)
            .OrderByDescending(b => b.SavedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Bookmark?> GetBookmarkAsync(string userId, int paperId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(b => b.UserId == userId && b.PaperId == paperId);
    }
}
