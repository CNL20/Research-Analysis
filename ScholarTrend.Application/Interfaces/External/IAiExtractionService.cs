using ScholarTrend.Application.DTOs.TopicInsights;

namespace ScholarTrend.Application.Interfaces.External;

public interface IAiExtractionService
{
    /// <summary>
    /// Extracts methods, datasets, limitations, and future work from a paper's abstract.
    /// </summary>
    Task<AiPaperExtractionDto?> ExtractFromAbstractAsync(string abstractText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Summarizes multiple future work snippets into high-level research opportunities.
    /// </summary>
    Task<List<AiOpportunityDto>> SummarizeOpportunitiesAsync(string topicName, List<string> futureWorks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates missing insights directly from the topic name when there isn't enough extracted data.
    /// </summary>
    Task<AiTopicFallbackDto?> GenerateFallbackInsightsAsync(string topicName, bool needMethods, bool needDatasets, bool needOpportunities, CancellationToken cancellationToken = default);
}
