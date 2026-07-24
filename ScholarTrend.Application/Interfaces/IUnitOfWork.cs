using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.Repositories;

namespace ScholarTrend.Application.Interfaces;

/// <summary>
/// Unit of Work pattern to coordinate multiple repository operations in a single transaction.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    DbContext Context { get; }
    IResearchPaperRepository ResearchPapers { get; }
    IBookmarkRepository Bookmarks { get; }
    IResearchTopicRepository Topics { get; }
    IJournalRepository Journals { get; }
    IAuthorRepository Authors { get; }
    ISearchHistoryRepository SearchHistories { get; }
    IFollowRepository Follows { get; }
    INotificationRepository Notifications { get; }
    IApiDataSourceRepository ApiDataSources { get; }
    ISyncLogRepository SyncLogs { get; }
    ISyncProposalRepository SyncProposals { get; }
    IPendingPaperRepository PendingPapers { get; }
    IPaperPdfFileRepository PaperPdfFiles { get; }
    IUserFileRepository UserFiles { get; }

    // Paper Analysis & Quality
    IPaperQualityRepository PaperQualities { get; }
    IPaperAnalysisRepository PaperAnalyses { get; }
    IAnalysisJobRepository AnalysisJobs { get; }
    
    // Pattern Mining
    IPatternRepository Patterns { get; }
    
    // Research Gap
    IResearchGapRepository ResearchGaps { get; }
    IGapTimelineRepository GapTimelines { get; }
    ICoverageReportRepository CoverageReports { get; }
    
    Task<int> SaveChangesAsync();

    Task<bool> BeginTransactionAsync(System.Data.IsolationLevel isolationLevel = System.Data.IsolationLevel.ReadCommitted);
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
