namespace ScholarTrend.Domain.Entities;

/// <summary>
/// Represents one source-record for a <see cref="ResearchPaper"/>.
/// A single ResearchPaper can be referenced from multiple sources
/// (ArXiv, OpenAlex, Crossref, SemanticScholar) via its DOI.
/// </summary>
public class PaperSource
{
    public int PaperId { get; set; }
    public ResearchPaper Paper { get; set; } = null!;
    public string SourceName { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string? SourceDoi { get; set; }
    public string? SourceUrl { get; set; }
    public int? SourceCitationCount { get; set; }
    public int? SourceYear { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public string? RawMetadataJson { get; set; }
}
