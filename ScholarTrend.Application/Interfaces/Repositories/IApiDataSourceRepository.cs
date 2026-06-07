using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface IApiDataSourceRepository
{
    Task<IReadOnlyList<ApiDataSource>> GetAllAsync();
    Task<ApiDataSource?> GetByIdAsync(int id);
    Task<ApiDataSource?> GetByNameAsync(string name);
    Task<IReadOnlyList<ApiDataSource>> GetActiveAsync();
    void Update(ApiDataSource source);
}
