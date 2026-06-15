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
        var items = await query
            .OrderByDescending(p => p.CitationCount)
            .ThenByDescending(p => p.PublicationYear)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<IEnumerable<ResearchPaper>> GetPapersByTopicAsync(int topicId, int limit = 0)
    {
        var query = _dbSet
            .Where(p => p.Status == PaperStatus.Available && p.PaperTopics.Any(pt => pt.TopicId == topicId))
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
            .Where(p => p.JournalId == journalId && p.Status == PaperStatus.Available)
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
            .Where(p => p.Status == PaperStatus.Available && p.PaperAuthors.Any(pa => pa.AuthorId == authorId))
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
            .FirstOrDefaultAsync(p => p.Id == id && p.Status == PaperStatus.Available);
    }

    public Task<int> CountByTopicAsync(int topicId)
    {
        return _context.PaperTopics
            .CountAsync(pt => pt.TopicId == topicId && pt.Paper.Status == PaperStatus.Available);
    }

    public Task<int> CountByJournalAsync(int journalId)
    {
        return _dbSet.CountAsync(p => p.JournalId == journalId && p.Status == PaperStatus.Available);
    }

    public Task<int> CountByAuthorAsync(int authorId)
    {
        return _context.PaperAuthors
            .CountAsync(pa => pa.AuthorId == authorId && pa.Paper.Status == PaperStatus.Available);
    }

    public Task<ResearchPaper?> GetByExternalIdAsync(string externalId, string source)
    {
        return _dbSet.FirstOrDefaultAsync(p => p.ExternalId == externalId && p.ExternalSource == source);
    }

    private IQueryable<ResearchPaper> BuildSearchQuery(PaperSearchCriteria criteria)
    {
        var query = _dbSet
            .Include(p => p.Journal)
            .Include(p => p.PaperAuthors).ThenInclude(pa => pa.Author)
            .Include(p => p.PaperKeywords).ThenInclude(pk => pk.Keyword)
            .Where(p => p.Status == PaperStatus.Available);

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
                "all" => query.Where(p =>
                    p.Title.Contains(term) ||
                    (p.Abstract != null && p.Abstract.Contains(term)) ||
                    p.PaperAuthors.Any(pa => pa.Author.Name.Contains(term)) ||
                    (p.Journal != null && p.Journal.Name.Contains(term)) ||
                    p.PaperKeywords.Any(pk => pk.Keyword.Name.Contains(term))),
                _ => query.Where(p =>
                    p.Title.Contains(term) ||
                    (p.Abstract != null && p.Abstract.Contains(term)) ||
                    p.PaperKeywords.Any(pk => pk.Keyword.Name.Contains(term)))
            };
        }

        return query;
    }
}
