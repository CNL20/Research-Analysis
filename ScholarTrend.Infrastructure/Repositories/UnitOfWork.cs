using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Infrastructure.Data;
using ScholarTrend.Infrastructure.Persistence.Repositories;

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
    private IAuthorRepository? _authors;
    private ISearchHistoryRepository? _searchHistories;
    private IFollowRepository? _follows;
    private INotificationRepository? _notifications;
    private IApiDataSourceRepository? _apiDataSources;
    private ISyncLogRepository? _syncLogs;
    private ISyncProposalRepository? _syncProposals;
    private IPendingPaperRepository? _pendingPapers;
    private IPaperPdfFileRepository? _paperPdfFiles;
    private IPaperQualityRepository? _paperQualities;
    private IPaperAnalysisRepository? _paperAnalyses;
    private IAnalysisJobRepository? _analysisJobs;
    private IPatternRepository? _patterns;
    private IResearchGapRepository? _researchGaps;
    private IGapTimelineRepository? _gapTimelines;
    private ICoverageReportRepository? _coverageReports;
    private IUserFileRepository? _userFiles;
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _transaction;

    public DbContext Context => _context;

    public UnitOfWork(ScholarTrendDbContext context)
    {
        _context = context;
    }

    public IResearchPaperRepository ResearchPapers => _researchPapers ??= new ResearchPaperRepository(_context);
    public IBookmarkRepository Bookmarks => _bookmarks ??= new BookmarkRepository(_context);
    public IResearchTopicRepository Topics => _topics ??= new ResearchTopicRepository(_context);
    public IJournalRepository Journals => _journals ??= new JournalRepository(_context);
    public IAuthorRepository Authors => _authors ??= new AuthorRepository(_context);
    public ISearchHistoryRepository SearchHistories => _searchHistories ??= new SearchHistoryRepository(_context);
    public IFollowRepository Follows => _follows ??= new FollowRepository(_context);
    public INotificationRepository Notifications => _notifications ??= new NotificationRepository(_context);
    public IApiDataSourceRepository ApiDataSources => _apiDataSources ??= new ApiDataSourceRepository(_context);
    public ISyncLogRepository SyncLogs => _syncLogs ??= new SyncLogRepository(_context);
    public ISyncProposalRepository SyncProposals => _syncProposals ??= new SyncProposalRepository(_context);
    public IPendingPaperRepository PendingPapers => _pendingPapers ??= new PendingPaperRepository(_context);
    public IPaperPdfFileRepository PaperPdfFiles => _paperPdfFiles ??= new PaperPdfFileRepository(_context);
    public IPaperQualityRepository PaperQualities => _paperQualities ??= new PaperQualityRepository(_context);
    public IPaperAnalysisRepository PaperAnalyses => _paperAnalyses ??= new PaperAnalysisRepository(_context);
    public IAnalysisJobRepository AnalysisJobs => _analysisJobs ??= new AnalysisJobRepository(_context);
    public IPatternRepository Patterns => _patterns ??= new PatternRepository(_context);
    public IResearchGapRepository ResearchGaps => _researchGaps ??= new ResearchGapRepository(_context);
    public IGapTimelineRepository GapTimelines => _gapTimelines ??= new GapTimelineRepository(_context);
    public ICoverageReportRepository CoverageReports => _coverageReports ??= new CoverageReportRepository(_context);
    public IUserFileRepository UserFiles => _userFiles ??= new UserFileRepository(_context);

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public async Task<bool> BeginTransactionAsync()
    {
        if (_transaction != null)
        {
            return false;
        }
        _transaction = await _context.Database.BeginTransactionAsync();
        return true;
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
        {
            await _context.SaveChangesAsync();
            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
