using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface ICoverageReportRepository
{
    Task<CoverageReport?> GetLatestByTopicIdAsync(int topicId);
    Task<CoverageReport?> GetByIdAsync(int id);
    Task AddAsync(CoverageReport report);
    Task UpdateAsync(CoverageReport report);
}
