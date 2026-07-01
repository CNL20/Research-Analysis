using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Constants;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

public class UserFileRepository : IUserFileRepository
{
    private readonly ScholarTrendDbContext _context;

    public UserFileRepository(ScholarTrendDbContext context)
    {
        _context = context;
    }

    public Task<UserFile?> GetByIdAsync(int id)
    {
        return _context.UserFiles
            .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted);
    }

    public async Task<(IReadOnlyList<UserFile> Items, int TotalCount)> GetUserFilesAsync(
        string userId,
        string? category,
        int page,
        int pageSize)
    {
        var query = _context.UserFiles
            .Where(f => f.UserId == userId && !f.IsDeleted);

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(f => f.Category == category);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public Task<int> CountActiveByUserAsync(string userId)
    {
        return _context.UserFiles.CountAsync(f => f.UserId == userId && !f.IsDeleted);
    }

    public async Task<IReadOnlyList<UserFile>> GetActiveAvatarsByUserAsync(string userId)
    {
        return await _context.UserFiles
            .Where(f => f.UserId == userId && f.Category == FileCategories.Avatar && !f.IsDeleted)
            .ToListAsync();
    }

    public async Task AddAsync(UserFile file)
    {
        await _context.UserFiles.AddAsync(file);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(UserFile file)
    {
        _context.UserFiles.Update(file);
        await _context.SaveChangesAsync();
    }
}
