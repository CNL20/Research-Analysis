using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface IBookmarkRepository : IGenericRepository<Bookmark>
{
    Task<IEnumerable<Bookmark>> GetUserBookmarksAsync(string userId);
    Task<Bookmark?> GetBookmarkAsync(string userId, int paperId);
}
