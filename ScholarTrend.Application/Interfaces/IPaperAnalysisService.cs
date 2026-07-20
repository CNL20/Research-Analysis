using ScholarTrend.Application.DTOs.GapAnalysis;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces;

public interface IPaperAnalysisService
{
    Task<PaperAnalysis?> AnalyzePaperAsync(int paperId, CancellationToken ct = default);
    Task<PaperAnalysis?> GetAnalysisAsync(int paperId);
    Task<int> AnalyzePapersByTopicAsync(int topicId, CancellationToken ct = default);
    Task<GapAnalysisResultDto> GetAnalysisResultAsync(int topicId);
}
