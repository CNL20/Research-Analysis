using ScholarTrend.Application.Interfaces;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

/// <summary>
/// Unit of Work implementation coordinating SaveChanges across repositories.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly ScholarTrendDbContext _context;
    private IResearchPaperRepository? _researchPapers;
    private IBookmarkRepository? _bookmarks;
    private IResearchTopicRepository? _topics;
    private IJournalRepository? _journals;

    public UnitOfWork(ScholarTrendDbContext context)
    {
        _context = context;
    }

    public IResearchPaperRepository ResearchPapers => _researchPapers ??= new ResearchPaperRepository(_context);
    public IBookmarkRepository Bookmarks => _bookmarks ??= new BookmarkRepository(_context);
    public IResearchTopicRepository Topics => _topics ??= new ResearchTopicRepository(_context);
    public IJournalRepository Journals => _journals ??= new JournalRepository(_context);

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
