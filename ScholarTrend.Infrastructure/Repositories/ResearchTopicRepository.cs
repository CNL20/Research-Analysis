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
        return await _dbSet.FirstOrDefaultAsync(t => t.Name == name);
    }
}
