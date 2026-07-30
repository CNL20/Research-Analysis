using ScholarTrend.Application.DTOs.GapAnalysis;

namespace ScholarTrend.Application.Interfaces;

public interface IResearchGapService
{
    /// <summary>Read-only report from stored gaps/patterns (no AI).</summary>
    Task<ResearchGapReportDto> GetGapReportAsync(int topicId, CancellationToken ct = default);

    /// <summary>
    /// Generate (or reuse cache unless <paramref name="force"/>).
    /// Uses Top-K prompt trimming and year-chunking for large topics.
    /// </summary>
    Task<ResearchGapReportDto> GenerateGapReportAsync(
        int topicId,
        bool force = false,
        CancellationToken ct = default);

    Task<List<ResearchGapDto>> GetGapsAsync(int topicId);
    Task<ResearchGapDetailDto?> GetGapDetailAsync(int gapId);
    Task<List<ResearchGapEvidenceDto>> GetGapEvidencesAsync(int gapId);
}
