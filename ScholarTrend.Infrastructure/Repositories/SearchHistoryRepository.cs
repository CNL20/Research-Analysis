using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

public class SearchHistoryRepository : ISearchHistoryRepository
{
    private readonly ScholarTrendDbContext _context;

    public SearchHistoryRepository(ScholarTrendDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SearchHistory history)
    {
        await _context.SearchHistories.AddAsync(history);
    }

    public async Task<IReadOnlyList<SearchHistory>> GetRecentByUserAsync(string userId, int limit = 20)
    {
        return await _context.SearchHistories
            .Where(sh => sh.UserId == userId)
            .OrderByDescending(sh => sh.SearchedAt)
            .Take(limit)
            .ToListAsync();
    }
}
