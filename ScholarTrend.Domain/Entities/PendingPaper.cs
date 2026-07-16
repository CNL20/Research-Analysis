namespace ScholarTrend.Domain.Entities;

public class PendingPaper
{
    public int Id { get; set; }
    public int SyncProposalId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string ExternalSource { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Abstract { get; set; }
    public int? Year { get; set; }
    public int? CitationCount { get; set; }
    public string? Doi { get; set; }
    public string? Url { get; set; }
    public string AuthorNamesJson { get; set; } = "[]";
    public string Status { get; set; } = Constants.PendingPaperStatus.Pending;
    public int? ImportedPaperId { get; set; }
    public string? PdfUrl { get; set; }
    public string? PdfAccessType { get; set; }
    public string? PdfLicense { get; set; }
    public string? SyncSearchQuery { get; set; }
    public string? JournalName { get; set; }
    public string KeywordsJson { get; set; } = "[]";

    public SyncProposal SyncProposal { get; set; } = null!;
}
