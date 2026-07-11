namespace ScholarTrend.Application.DTOs.Aggregation;

/// <summary>
/// Normalized metadata from a single data source for comparison.
/// </summary>
public class PaperSourceMetadataDto
{
    public string Source { get; set; } = string.Empty;
    public bool Found { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ExternalId { get; set; }
    public string? Doi { get; set; }
    public string? Title { get; set; }
    public int? Year { get; set; }
    public string? Journal { get; set; }
    public string? Url { get; set; }
    public List<string> Authors { get; set; } = [];
    public string? Abstract { get; set; }
    public int? CitationCount { get; set; }
    public List<string> Keywords { get; set; } = [];
    public string? PdfUrl { get; set; }
    public string? ArxivId { get; set; }
}
