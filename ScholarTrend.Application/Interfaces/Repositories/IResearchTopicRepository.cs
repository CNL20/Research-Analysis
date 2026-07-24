using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface IResearchTopicRepository : IGenericRepository<ResearchTopic>
{
    Task<ResearchTopic?> GetByNameAsync(string name);
    Task<(IReadOnlyList<ResearchTopic> Items, int TotalCount)> GetPagedAsync(string? keyword, int page, int pageSize);
}
