using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

public class ResearchTopicRepository : GenericRepository<ResearchTopic>, IResearchTopicRepository
{
    public ResearchTopicRepository(ScholarTrendDbContext context) : base(context)
    {
    }

    public async Task<ResearchTopic?> GetByNameAsync(string name)
    {
        return await _dbSet.FirstOrDefaultAsync(t => t.TopicName == name);
    }

    public async Task<(IReadOnlyList<ResearchTopic> Items, int TotalCount)> GetPagedAsync(string? keyword, int page, int pageSize)
    {
        var query = _dbSet.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lowerKeyword = keyword.Trim().ToLower();
            query = query.Where(t => 
                t.TopicName.ToLower().Contains(lowerKeyword) || 
                (t.Description != null && t.Description.ToLower().Contains(lowerKeyword)));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}
