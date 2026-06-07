using ScholarTrend.Application.DTOs.Bookmarks;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Services;

public class BookmarkService : IBookmarkService
{
    private readonly IUnitOfWork _unitOfWork;

    public BookmarkService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<BookmarkDto>> GetUserBookmarksAsync(string userId)
    {
        var bookmarks = await _unitOfWork.Bookmarks.GetUserBookmarksAsync(userId);
        return bookmarks.Select(b => new BookmarkDto
        {
            Id = b.Id,
            PaperId = b.PaperId,
            Title = b.Paper.Title,
            PublicationYear = b.Paper.PublicationYear,
            CitationCount = b.Paper.CitationCount,
            JournalName = b.Paper.Journal?.Name,
            SavedAt = b.SavedAt
        }).ToList();
    }

    public async Task<BookmarkDto> AddBookmarkAsync(string userId, int paperId)
    {
        var paper = await _unitOfWork.ResearchPapers.GetPaperWithDetailsAsync(paperId);
        if (paper == null)
        {
            throw new InvalidOperationException("Paper not found.");
        }

        var existing = await _unitOfWork.Bookmarks.GetBookmarkAsync(userId, paperId);
        if (existing != null)
        {
            throw new InvalidOperationException("Paper is already bookmarked.");
        }

        var bookmark = new Bookmark
        {
            UserId = userId,
            PaperId = paperId,
            SavedAt = DateTime.UtcNow
        };

        await _unitOfWork.Bookmarks.AddAsync(bookmark);
        await _unitOfWork.SaveChangesAsync();

        return new BookmarkDto
        {
            Id = bookmark.Id,
            PaperId = paperId,
            Title = paper.Title,
            PublicationYear = paper.PublicationYear,
            CitationCount = paper.CitationCount,
            JournalName = paper.Journal?.Name,
            SavedAt = bookmark.SavedAt
        };
    }

    public async Task RemoveBookmarkAsync(string userId, int paperId)
    {
        var bookmark = await _unitOfWork.Bookmarks.GetBookmarkAsync(userId, paperId);
        if (bookmark == null)
        {
            throw new InvalidOperationException("Bookmark not found.");
        }

        _unitOfWork.Bookmarks.Delete(bookmark);
        await _unitOfWork.SaveChangesAsync();
    }
}
