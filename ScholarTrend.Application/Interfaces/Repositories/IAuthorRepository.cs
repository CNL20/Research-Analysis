using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface IAuthorRepository : IGenericRepository<Author>
{
    Task<Author?> GetByNameAsync(string name);
}
