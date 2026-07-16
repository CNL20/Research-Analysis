namespace ScholarTrend.Application.Interfaces;

/// <summary>
/// Enqueues a Hangfire background job to enrich an existing
/// <see cref="Domain.Entities.ResearchPaper"/> with metadata from the
/// remaining bibliographic sources not yet captured in its PaperSources rows.
/// </summary>
public interface IEnrichPaperSourcesEnqueuer
{
    Task EnqueueEnrichmentAsync(int paperId, string? doi, string primarySource,
        CancellationToken ct = default);
}
