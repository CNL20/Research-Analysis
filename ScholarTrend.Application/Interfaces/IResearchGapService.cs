using ScholarTrend.Application.DTOs.GapAnalysis;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces;

public interface IResearchGapService
{
    Task<ResearchGapReportDto> GenerateGapReportAsync(int topicId, CancellationToken ct = default);
    Task<List<ResearchGapDto>> GetGapsAsync(int topicId);
    Task<ResearchGapDetailDto?> GetGapDetailAsync(int gapId);
    Task<List<ResearchGapEvidenceDto>> GetGapEvidencesAsync(int gapId);
}
