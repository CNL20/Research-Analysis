using ScholarTrend.Application.DTOs.TopicInsights;

namespace ScholarTrend.Application.Interfaces.External;

public interface IPdfAnalysisService
{
    /// <summary>
    /// Analyzes a paper's PDF: downloads (if needed), extracts text, sends to Gemini, caches result.
    /// Returns null if paper has no PdfUrl or if extraction fails.
    /// </summary>
    Task<AiPaperExtractionDto?> AnalyzePdfAsync(int researchPaperId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns cached extraction result if available. Returns null if not yet analyzed.
    /// </summary>
    Task<AiPaperExtractionDto?> GetCachedAnalysisAsync(int researchPaperId, CancellationToken cancellationToken = default);
}
