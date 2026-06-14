using ScholarTrend.Application.Interfaces.Repositories;

namespace ScholarTrend.Application.Interfaces;

/// <summary>
/// Unit of Work pattern to coordinate multiple repository operations in a single transaction.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IResearchPaperRepository ResearchPapers { get; }
    IBookmarkRepository Bookmarks { get; }
    IResearchTopicRepository Topics { get; }
    IJournalRepository Journals { get; }
    ISearchHistoryRepository SearchHistories { get; }
    IFollowRepository Follows { get; }
    INotificationRepository Notifications { get; }
    IApiDataSourceRepository ApiDataSources { get; }
    ISyncLogRepository SyncLogs { get; }
    ISyncProposalRepository SyncProposals { get; }
    Task<int> SaveChangesAsync();
}
