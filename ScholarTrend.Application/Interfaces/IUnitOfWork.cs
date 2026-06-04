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
    Task<int> SaveChangesAsync();
}
