using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
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
    private ISearchHistoryRepository? _searchHistories;
    private IFollowRepository? _follows;
    private INotificationRepository? _notifications;
    private IApiDataSourceRepository? _apiDataSources;
    private ISyncLogRepository? _syncLogs;
    private ISyncProposalRepository? _syncProposals;

    public UnitOfWork(ScholarTrendDbContext context)
    {
        _context = context;
    }

    public IResearchPaperRepository ResearchPapers => _researchPapers ??= new ResearchPaperRepository(_context);
    public IBookmarkRepository Bookmarks => _bookmarks ??= new BookmarkRepository(_context);
    public IResearchTopicRepository Topics => _topics ??= new ResearchTopicRepository(_context);
    public IJournalRepository Journals => _journals ??= new JournalRepository(_context);
    public ISearchHistoryRepository SearchHistories => _searchHistories ??= new SearchHistoryRepository(_context);
    public IFollowRepository Follows => _follows ??= new FollowRepository(_context);
    public INotificationRepository Notifications => _notifications ??= new NotificationRepository(_context);
    public IApiDataSourceRepository ApiDataSources => _apiDataSources ??= new ApiDataSourceRepository(_context);
    public ISyncLogRepository SyncLogs => _syncLogs ??= new SyncLogRepository(_context);
    public ISyncProposalRepository SyncProposals => _syncProposals ??= new SyncProposalRepository(_context);

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
