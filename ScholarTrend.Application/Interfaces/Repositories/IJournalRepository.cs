using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface IJournalRepository : IGenericRepository<Journal>
{
    Task<Journal?> GetByIssnAsync(string issn);
}
