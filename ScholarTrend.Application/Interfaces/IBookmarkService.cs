using ScholarTrend.Application.DTOs.Bookmarks;

namespace ScholarTrend.Application.Interfaces;

public interface IBookmarkService
{
    Task<IReadOnlyList<BookmarkDto>> GetUserBookmarksAsync(string userId);
    Task<BookmarkDto> AddBookmarkAsync(string userId, int paperId);
    Task RemoveBookmarkAsync(string userId, int paperId);
}
