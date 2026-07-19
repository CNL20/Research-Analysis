using ScholarTrend.Application.DTOs.GapAnalysis;

namespace ScholarTrend.Application.Interfaces;

public interface ICoverageReportService
{
    Task<CoverageReportDto> GenerateReportAsync(int topicId);
    Task<CoverageReportDto?> GetLatestReportAsync(int topicId);
    Task<PaperQualityReportDto> GetQualityReportAsync(int topicId);
}
