using ScholarTrend.Application.DTOs.GapAnalysis;
using ScholarTrend.Application.DTOs.TopicInsights;

namespace ScholarTrend.Application.Interfaces.External;

public interface IAiExtractionService
{
    /// <summary>
    /// Extracts methods, datasets, limitations, and future work from a paper's abstract.
    /// </summary>
    Task<AiPaperExtractionDto?> ExtractFromAbstractAsync(string abstractText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts detailed information from full text or sections including Discussion, Conclusion, Future Work, Limitations.
    /// </summary>
    Task<AiPaperExtractionDto?> ExtractFromFullTextAsync(string fullText, CancellationToken cancellationToken = default);

    /// <summary>
    /// HYBRID EXTRACTION: First extracts from abstract, then conditionally extracts from targeted sections
    /// based on which fields are missing. This approach balances accuracy with cost efficiency.
    /// For committee defense: provides transparent, traceable evidence from multiple sources.
    /// </summary>
    Task<HybridExtractionResultDto?> ExtractHybridAsync(
        string abstractText,
        string? discussionSection,
        string? conclusionSection,
        string? introductionSection,
        string? methodologySection,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts missing fields from targeted sections. Used when abstract extraction is incomplete.
    /// </summary>
    Task<AiPaperExtractionDto?> ExtractMissingFieldsAsync(
        List<string> missingFields,
        string? discussionSection,
        string? conclusionSection,
        string? introductionSection,
        string? methodologySection,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Infers limitations and future work when extraction returns empty results.
    /// Uses paper title, abstract, methods, and datasets to critically analyze and generate insights.
    /// </summary>
    Task<AiPaperExtractionDto> InferLimitationsAndFutureWorkAsync(
        string paperTitle,
        string abstractText,
        List<string> methods,
        List<string> datasets,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Summarizes multiple future work snippets into high-level research opportunities.
    /// </summary>
    Task<List<AiOpportunityDto>> SummarizeOpportunitiesAsync(string topicName, List<string> futureWorks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates missing insights directly from the topic name when there isn't enough extracted data.
    /// </summary>
    Task<AiTopicFallbackDto?> GenerateFallbackInsightsAsync(string topicName, bool needMethods, bool needDatasets, bool needOpportunities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates research gaps based on patterns, trends, and paper analyses.
    /// </summary>
    Task<List<ResearchGapDto>> GenerateResearchGapsAsync(
        string topicName,
        PatternMiningResultDto patterns,
        GapTimelineDto timeline,
        List<PaperAnalysisDto> analyses,
        CancellationToken cancellationToken = default);
}
