namespace ScholarTrend.Application.DTOs.Sync;

public class PendingPaperDto
{
    public int Id { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string ExternalSource { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Abstract { get; set; }
    public int? Year { get; set; }
    public int? CitationCount { get; set; }
    public string? Doi { get; set; }
    public string? Url { get; set; }
    public List<string> Authors { get; set; } = [];
    public string Status { get; set; } = string.Empty;
    public int? ImportedPaperId { get; set; }
    public string? PdfUrl { get; set; }
    public string? PdfAccessType { get; set; }
    public string? PdfLicense { get; set; }
}
