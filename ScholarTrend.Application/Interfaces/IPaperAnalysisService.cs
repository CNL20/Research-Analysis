using ScholarTrend.Application.DTOs.GapAnalysis;
using ScholarTrend.Application.DTOs.TopicInsights;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces;

public interface IPaperAnalysisService
{
    Task<PaperAnalysis?> AnalyzePaperAsync(int paperId, CancellationToken ct = default);

    /// <summary>
    /// HYBRID ANALYSIS: Extracts from abstract + targeted sections (Discussion, Conclusion)
    /// for more comprehensive gap analysis. Recommended for committee defense.
    /// </summary>
    Task<PaperAnalysis?> AnalyzePaperHybridAsync(int paperId, string? fullText = null, CancellationToken ct = default);

    Task<PaperAnalysis?> GetAnalysisAsync(int paperId);

    /// <summary>
    /// Returns detailed hybrid extraction metadata including confidence breakdown.
    /// </summary>
    Task<HybridExtractionResultDto?> GetHybridAnalysisAsync(int paperId, CancellationToken ct = default);

    Task<int> AnalyzePapersByTopicAsync(int topicId, CancellationToken ct = default);

    /// <summary>
    /// Analyzes papers using hybrid approach for a specific topic.
    /// </summary>
    Task<int> AnalyzePapersByTopicHybridAsync(int topicId, CancellationToken ct = default);

    Task<GapAnalysisResultDto> GetAnalysisResultAsync(int topicId);
}
