using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces;

public interface IPaperQualityService
{
    Task<PaperQuality> AssessPaperAsync(int paperId);
    Task<PaperQuality> GetOrAssessAsync(int paperId);
    Task<Dictionary<string, int>> GetCoverageReportAsync(int topicId);
}
