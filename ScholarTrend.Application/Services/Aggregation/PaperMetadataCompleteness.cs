using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Services.Aggregation;

/// <summary>
/// Checks which bibliographic fields are still missing on a stored paper.
/// </summary>
public static class PaperMetadataCompleteness
{
    public static IReadOnlyList<string> GetMissingFields(ResearchPaper paper)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(paper.Doi))
        {
            missing.Add("doi");
        }

        if (string.IsNullOrWhiteSpace(paper.Abstract))
        {
            missing.Add("abstract");
        }

        if (paper.JournalId == null)
        {
            missing.Add("journal");
        }

        if (paper.PaperAuthors.Count == 0)
        {
            missing.Add("authors");
        }

        if (paper.PaperKeywords.Count == 0)
        {
            missing.Add("keywords");
        }

        if (string.IsNullOrWhiteSpace(paper.PdfUrl))
        {
            missing.Add("pdfUrl");
        }

        return missing;
    }
}
