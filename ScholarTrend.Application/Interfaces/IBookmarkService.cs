using ScholarTrend.Application.DTOs.Bookmarks;
using ScholarTrend.Application.DTOs.Common;

namespace ScholarTrend.Application.Interfaces;

public interface IBookmarkService
{
    Task<PagedResult<BookmarkDto>> GetUserBookmarksAsync(string userId, int page = 1, int pageSize = 10);
    Task<BookmarkDto> AddBookmarkAsync(string userId, int paperId);
    Task RemoveBookmarkAsync(string userId, int paperId);
}
