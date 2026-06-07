using ScholarTrend.Application.DTOs.Papers;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Mappings;

public static class PaperMapper
{
    public static PaperListItemDto ToListItem(ResearchPaper paper)
    {
        return new PaperListItemDto
        {
            Id = paper.Id,
            Title = paper.Title,
            Abstract = TruncateAbstract(paper.Abstract),
            PublicationYear = paper.PublicationYear,
            CitationCount = paper.CitationCount,
            Doi = paper.Doi,
            Journal = paper.Journal == null ? null : new JournalBriefDto
            {
                Id = paper.Journal.Id,
                Name = paper.Journal.Name,
                Issn = paper.Journal.Issn
            },
            Authors = paper.PaperAuthors.Select(pa => pa.Author.Name).ToList(),
            Keywords = paper.PaperKeywords.Select(pk => pk.Keyword.Name).ToList()
        };
    }

    public static PaperDetailDto ToDetail(ResearchPaper paper, bool isBookmarked = false)
    {
        return new PaperDetailDto
        {
            Id = paper.Id,
            Title = paper.Title,
            Abstract = paper.Abstract,
            PublicationYear = paper.PublicationYear,
            PublicationDate = paper.PublicationDate,
            CitationCount = paper.CitationCount,
            Doi = paper.Doi,
            Url = paper.Url,
            PdfUrl = paper.PdfUrl,
            Journal = paper.Journal == null ? null : new JournalBriefDto
            {
                Id = paper.Journal.Id,
                Name = paper.Journal.Name,
                Issn = paper.Journal.Issn
            },
            Authors = paper.PaperAuthors.Select(pa => new AuthorBriefDto
            {
                Id = pa.Author.Id,
                Name = pa.Author.Name,
                Affiliation = pa.Author.Affiliation
            }).ToList(),
            Keywords = paper.PaperKeywords.Select(pk => pk.Keyword.Name).ToList(),
            Topics = paper.PaperTopics.Select(pt => pt.Topic.TopicName).ToList(),
            IsBookmarked = isBookmarked
        };
    }

    private static string? TruncateAbstract(string? abstractText, int maxLength = 300)
    {
        if (string.IsNullOrEmpty(abstractText) || abstractText.Length <= maxLength)
        {
            return abstractText;
        }

        return abstractText[..maxLength] + "...";
    }
}
