using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface IAnalysisJobRepository
{
    Task<AnalysisJob?> GetByIdAsync(int id);
    Task<AnalysisJob?> GetPendingByPaperIdAsync(int paperId);
    Task<List<AnalysisJob>> GetPendingJobsAsync(int take = 50);
    Task<List<AnalysisJob>> GetByStatusAsync(string status, int take = 50);
    Task AddAsync(AnalysisJob job);
    void Update(AnalysisJob job);
}
