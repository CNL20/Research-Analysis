using ScholarTrend.Application.DTOs.Aggregation;

namespace ScholarTrend.Application.Interfaces.External;

public class ExternalPaperDto
{
    public string ExternalId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Abstract { get; set; }
    public int? Year { get; set; }
    public int? CitationCount { get; set; }
    public string? Doi { get; set; }
    public string? Url { get; set; }
    public string? Journal { get; set; }
    public List<string> AuthorNames { get; set; } = [];
    public List<string> Keywords { get; set; } = [];
    public string? PdfUrl { get; set; }
    public string? PdfAccessType { get; set; }
    public string? PdfLicense { get; set; }
}

public interface ISemanticScholarClient
{
    Task<IReadOnlyList<ExternalPaperDto>> SearchPapersAsync(string query, int limit = 20);
    Task<PaperSourceMetadataDto> GetByDoiAsync(string doi);
}

public interface IOpenAlexClient
{
    Task<IReadOnlyList<ExternalPaperDto>> SearchPapersAsync(string query, int limit = 20);
    Task<PaperSourceMetadataDto> GetByDoiAsync(string doi);
}

public interface ICrossrefClient
{
    Task<IReadOnlyList<ExternalPaperDto>> SearchPapersAsync(string query, int limit = 20);
    Task<PaperSourceMetadataDto> GetByDoiAsync(string doi);
}

public interface IArXivClient
{
    Task<IReadOnlyList<ExternalPaperDto>> SearchPapersAsync(string query, int limit = 20);
    Task<PaperSourceMetadataDto> GetByDoiAsync(string doi);
}
