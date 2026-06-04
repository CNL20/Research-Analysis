using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface IResearchTopicRepository : IGenericRepository<ResearchTopic>
{
    Task<ResearchTopic?> GetByNameAsync(string name);
}
