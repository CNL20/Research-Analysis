using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface IAuthorRepository : IGenericRepository<Author>
{
    Task<Author?> GetByNameAsync(string name);
    Task<(IReadOnlyList<Author> Items, int TotalCount)> GetPagedAsync(string? keyword, int page, int pageSize);
}
