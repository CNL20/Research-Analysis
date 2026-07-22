using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.DTOs.Papers;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Domain.Enums;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

public class ResearchPaperRepository : GenericRepository<ResearchPaper>, IResearchPaperRepository
{
    public ResearchPaperRepository(ScholarTrendDbContext context) : base(context)
    {
    }

    public async Task<(IReadOnlyList<ResearchPaper> Items, int TotalCount)> SearchAsync(PaperSearchCriteria criteria)
    {
        var query = BuildSearchQuery(criteria);

        var total = await query.CountAsync();

        var sortBy = (criteria.SortBy ?? "citations").Trim().ToLowerInvariant();
        var ordered = sortBy switch
        {
            // Newest imported/approved: CreatedAt is set on approve; Id DESC as tie-breaker
            // (Id alone is usually fine with identity columns, but CreatedAt is the real "when approved").
            "newest" or "id" => query
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id),
            "publish" => query
                .OrderByDescending(p => p.PublicationYear)
                .ThenByDescending(p => p.PublicationDate)
                .ThenByDescending(p => p.CitationCount),
            _ => query
                .OrderByDescending(p => p.CitationCount)
                .ThenByDescending(p => p.PublicationYear)
        };

        // Legacy: SearchType=publish also sorted by publish date when SortBy not overridden.
        if (sortBy is not ("newest" or "id" or "publish")
            && criteria.SearchType.Equals("publish", StringComparison.OrdinalIgnoreCase))
        {
            ordered = query
                .OrderByDescending(p => p.PublicationYear)
                .ThenByDescending(p => p.PublicationDate)
                .ThenByDescending(p => p.CitationCount);
        }

        var items = await ordered
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<IEnumerable<ResearchPaper>> GetPapersByTopicAsync(int topicId, int limit = 0)
    {
        var query = _dbSet
            .Where(p => PaperStatusRules.Browsable.Contains(p.Status) && p.PaperTopics.Any(pt => pt.TopicId == topicId))
            .Include(p => p.Journal)
            .Include(p => p.PaperAuthors).ThenInclude(pa => pa.Author)
            .Include(p => p.PaperKeywords).ThenInclude(pk => pk.Keyword)
            .OrderByDescending(p => p.CitationCount);

        return limit > 0
            ? await query.Take(limit).ToListAsync()
            : await query.ToListAsync();
    }

    public async Task<IEnumerable<ResearchPaper>> GetPapersByJournalAsync(int journalId, int limit = 0)
    {
        var query = _dbSet
            .Where(p => p.JournalId == journalId && PaperStatusRules.Browsable.Contains(p.Status))
            .Include(p => p.Journal)
            .Include(p => p.PaperAuthors).ThenInclude(pa => pa.Author)
            .Include(p => p.PaperKeywords).ThenInclude(pk => pk.Keyword)
            .OrderByDescending(p => p.CitationCount);

        if (limit > 0)
        {
            return await query.Take(limit).ToListAsync();
        }

        return await query.ToListAsync();
    }

    public async Task<IEnumerable<ResearchPaper>> GetPapersByAuthorAsync(int authorId, int limit = 0)
    {
        var query = _dbSet
            .Where(p => PaperStatusRules.Browsable.Contains(p.Status) && p.PaperAuthors.Any(pa => pa.AuthorId == authorId))
            .Include(p => p.Journal)
            .Include(p => p.PaperAuthors).ThenInclude(pa => pa.Author)
            .Include(p => p.PaperKeywords).ThenInclude(pk => pk.Keyword)
            .OrderByDescending(p => p.CitationCount)
            .ThenByDescending(p => p.PublicationYear);

        return limit > 0
            ? await query.Take(limit).ToListAsync()
            : await query.ToListAsync();
    }

    public async Task<ResearchPaper?> GetPaperWithDetailsAsync(int id)
    {
        return await _dbSet
            .Include(p => p.Journal)
            .Include(p => p.PaperAuthors).ThenInclude(pa => pa.Author)
            .Include(p => p.PaperKeywords).ThenInclude(pk => pk.Keyword)
            .Include(p => p.PaperTopics).ThenInclude(pt => pt.Topic)
            .FirstOrDefaultAsync(p => p.Id == id && PaperStatusRules.Browsable.Contains(p.Status));
    }

    public Task<int> CountByTopicAsync(int topicId)
    {
        return _context.PaperTopics
            .CountAsync(pt => pt.TopicId == topicId && PaperStatusRules.Browsable.Contains(pt.Paper.Status));
    }

    public Task<int> CountByJournalAsync(int journalId)
    {
        return _dbSet.CountAsync(p => p.JournalId == journalId && PaperStatusRules.Browsable.Contains(p.Status));
    }

    public Task<int> CountByAuthorAsync(int authorId)
    {
        return _context.PaperAuthors
            .CountAsync(pa => pa.AuthorId == authorId && PaperStatusRules.Browsable.Contains(pa.Paper.Status));
    }

    public Task<ResearchPaper?> GetByExternalIdAsync(string externalId, string source)
    {
        return _dbSet
            .Include(p => p.PaperSources)
            .FirstOrDefaultAsync(p =>
                p.PaperSources.Any(ps =>
                    ps.SourceName == source && ps.ExternalId == externalId));
    }

    public async Task<ResearchPaper?> GetByDoiAsync(string doi)
    {
        var normalizedDoi = doi.Trim().ToLowerInvariant();
        return await _dbSet
            .Include(p => p.Journal)
            .Include(p => p.PaperAuthors).ThenInclude(pa => pa.Author)
            .Include(p => p.PaperKeywords).ThenInclude(pk => pk.Keyword)
            .FirstOrDefaultAsync(p => p.Doi != null && p.Doi.ToLower() == normalizedDoi);
    }

    private IQueryable<ResearchPaper> BuildSearchQuery(PaperSearchCriteria criteria)
    {
        var query = _dbSet
            .Include(p => p.Journal)
            .Include(p => p.PaperAuthors).ThenInclude(pa => pa.Author)
            .Include(p => p.PaperKeywords).ThenInclude(pk => pk.Keyword)
            .Where(p => PaperStatusRules.Browsable.Contains(p.Status));

        if (criteria.JournalId.HasValue)
        {
            query = query.Where(p => p.JournalId == criteria.JournalId);
        }

        if (criteria.TopicId.HasValue)
        {
            query = query.Where(p => p.PaperTopics.Any(pt => pt.TopicId == criteria.TopicId));
        }

        if (criteria.YearFrom.HasValue)
        {
            query = query.Where(p => p.PublicationYear >= criteria.YearFrom);
        }

        if (criteria.YearTo.HasValue)
        {
            query = query.Where(p => p.PublicationYear <= criteria.YearTo);
        }

        if (criteria.MinCitations.HasValue)
        {
            query = query.Where(p => p.CitationCount >= criteria.MinCitations);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Query))
        {
            var term = criteria.Query.Trim();
            query = criteria.SearchType.ToLowerInvariant() switch
            {
                "author" => query.Where(p => p.PaperAuthors.Any(pa => pa.Author.Name.Contains(term))),
                "journal" => query.Where(p => p.Journal != null && p.Journal.Name.Contains(term)),
                "title" => query.Where(p => p.Title.Contains(term)),
                "publish" => ApplyPublishSearch(query, term),
                "all" => query.Where(p =>
                    p.Title.Contains(term) ||
                    (p.Abstract != null && p.Abstract.Contains(term)) ||
                    p.PaperAuthors.Any(pa => pa.Author.Name.Contains(term)) ||
                    (p.Journal != null && p.Journal.Name.Contains(term)) ||
                    p.PaperKeywords.Any(pk => pk.Keyword.Name.Contains(term)) ||
                    (p.PublicationYear != null && p.PublicationYear.ToString() == term)),
                _ => query.Where(p =>
                    p.Title.Contains(term) ||
                    (p.Abstract != null && p.Abstract.Contains(term)) ||
                    p.PaperKeywords.Any(pk => pk.Keyword.Name.Contains(term)))
            };
        }

        return query;
    }

    private static IQueryable<ResearchPaper> ApplyPublishSearch(IQueryable<ResearchPaper> query, string term)
    {
        if (!term.All(char.IsDigit))
        {
            return query.Where(p => false);
        }

        if (term.Length == 4 && int.TryParse(term, out var exactYear))
        {
            return query.Where(p => p.PublicationYear == exactYear);
        }

        if (term.Length is > 0 and < 4)
        {
            var minYear = int.Parse(term.PadRight(4, '0'));
            var maxYear = int.Parse(term.PadRight(4, '9'));
            return query.Where(p => p.PublicationYear >= minYear && p.PublicationYear <= maxYear);
        }

        return query.Where(p => false);
    }
}
