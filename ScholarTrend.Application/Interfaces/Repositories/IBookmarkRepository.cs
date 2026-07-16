using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface IBookmarkRepository : IGenericRepository<Bookmark>
{
    Task<(IEnumerable<Bookmark> Items, int TotalCount)> GetUserBookmarksAsync(string userId, int page = 1, int pageSize = 10);
    Task<Bookmark?> GetBookmarkAsync(string userId, int paperId);
}
