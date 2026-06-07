using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface ISearchHistoryRepository
{
    Task AddAsync(SearchHistory history);
    Task<IReadOnlyList<SearchHistory>> GetRecentByUserAsync(string userId, int limit = 20);
}
