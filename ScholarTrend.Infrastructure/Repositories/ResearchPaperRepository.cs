using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

public class ResearchPaperRepository : GenericRepository<ResearchPaper>, IResearchPaperRepository
{
    public ResearchPaperRepository(ScholarTrendDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<ResearchPaper>> GetPapersByTopicAsync(int topicId)
    {
        return await _context.PaperTopics
            .Where(pt => pt.TopicId == topicId)
            .Select(pt => pt.Paper)
            .ToListAsync();
    }

    public async Task<IEnumerable<ResearchPaper>> GetPapersByJournalAsync(int journalId)
    {
        return await _dbSet
            .Where(p => p.JournalId == journalId)
            .ToListAsync();
    }

    public async Task<IEnumerable<ResearchPaper>> SearchPapersAsync(string searchTerm)
    {
        return await _dbSet
            .Where(p => p.Title.Contains(searchTerm) || (p.Abstract != null && p.Abstract.Contains(searchTerm)))
            .ToListAsync();
    }

    public async Task<ResearchPaper?> GetPaperWithDetailsAsync(int id)
    {
        return await _dbSet
            .Include(p => p.Journal)
            .Include(p => p.PaperAuthors).ThenInclude(pa => pa.Author)
            .Include(p => p.PaperKeywords).ThenInclude(pk => pk.Keyword)
            .Include(p => p.PaperTopics).ThenInclude(pt => pt.Topic)
            .FirstOrDefaultAsync(p => p.Id == id);
    }
}
