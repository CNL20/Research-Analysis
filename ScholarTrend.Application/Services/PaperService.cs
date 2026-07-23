using System.Diagnostics;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.DTOs.Papers;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Mappings;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Services;

public class PaperService : IPaperService
{
    private readonly IUnitOfWork _unitOfWork;

    public PaperService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<PaperListItemDto>> SearchAsync(PaperSearchRequest request, string userId)
    {
        var stopwatch = Stopwatch.StartNew();
        var criteria = MapToCriteria(request);
        var (papers, totalCount) = await _unitOfWork.ResearchPapers.SearchAsync(criteria);
        stopwatch.Stop();

        await LogSearchHistoryAsync(userId, request, totalCount, (int)stopwatch.ElapsedMilliseconds);

        return new PagedResult<PaperListItemDto>
        {
            Items = papers.Select(PaperMapper.ToListItem).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<PaperDetailDto> GetByIdAsync(int id, string userId)
    {
        var paper = await _unitOfWork.ResearchPapers.GetPaperWithDetailsAsync(id);
        if (paper == null)
        {
            throw new InvalidOperationException("Paper not found.");
        }

        var bookmark = await _unitOfWork.Bookmarks.GetBookmarkAsync(userId, id);
        var detail = PaperMapper.ToDetail(paper, bookmark != null);

        // Lấy danh sách PDF do cộng đồng tải lên (UserFiles)
        var communityFiles = await _unitOfWork.UserFiles.GetByPaperIdAsync(id);
        detail.CommunityPdfs = communityFiles
            .Where(f => string.Equals(f.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase) 
                     || f.OriginalFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            .Select(f => new CommunityPdfDto
            {
                FileId = f.Id,
                FileName = f.OriginalFileName,
                UploadedByFullName = f.User?.FullName ?? "Unknown User",
                UploadedAt = f.CreatedAt,
                DownloadUrl = $"/api/files/{f.Id}/download"
            })
            .ToList();

        return detail;
    }

    public async Task<PagedResult<PaperListItemDto>> GetByTopicAsync(int topicId, int page, int pageSize)
    {
        var topic = await _unitOfWork.Topics.GetByIdAsync(topicId);
        if (topic == null)
        {
            throw new InvalidOperationException("Topic not found.");
        }

        var criteria = new PaperSearchCriteria
        {
            TopicId = topicId,
            Page = page,
            PageSize = pageSize
        };

        var (papers, totalCount) = await _unitOfWork.ResearchPapers.SearchAsync(criteria);
        return new PagedResult<PaperListItemDto>
        {
            Items = papers.Select(PaperMapper.ToListItem).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<PaperListItemDto>> GetByJournalAsync(int journalId, int page, int pageSize)
    {
        var journal = await _unitOfWork.Journals.GetByIdAsync(journalId);
        if (journal == null)
        {
            throw new InvalidOperationException("Journal not found.");
        }

        var criteria = new PaperSearchCriteria
        {
            JournalId = journalId,
            Page = page,
            PageSize = pageSize
        };

        var (papers, totalCount) = await _unitOfWork.ResearchPapers.SearchAsync(criteria);
        return new PagedResult<PaperListItemDto>
        {
            Items = papers.Select(PaperMapper.ToListItem).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<SearchHistoryDto>> GetSearchHistoryAsync(string userId, int limit = 20)
    {
        var history = await _unitOfWork.SearchHistories.GetRecentByUserAsync(userId, limit);
        return history.Select(h => new SearchHistoryDto
        {
            Id = h.Id,
            Query = h.Query,
            SearchType = h.SearchType,
            ResultCount = h.ResultCount,
            SearchedAt = h.SearchedAt
        }).ToList();
    }

    public async Task RecordViewAsync(int id)
    {
        var paper = await _unitOfWork.ResearchPapers.GetByIdAsync(id);
        if (paper == null)
        {
            throw new InvalidOperationException("Paper not found.");
        }

        paper.ViewCount += 1;
        _unitOfWork.ResearchPapers.Update(paper);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task LogSearchHistoryAsync(string userId, PaperSearchRequest request, int resultCount, int durationMs)
    {
        var history = new SearchHistory
        {
            UserId = userId,
            Query = request.Query ?? string.Empty,
            SearchType = request.SearchType,
            ResultCount = resultCount,
            DurationMs = durationMs,
            SearchedAt = DateTime.UtcNow
        };

        await _unitOfWork.SearchHistories.AddAsync(history);
        await _unitOfWork.SaveChangesAsync();
    }

    private static PaperSearchCriteria MapToCriteria(PaperSearchRequest request)
    {
        return new PaperSearchCriteria
        {
            Query = request.Query,
            SearchType = request.SearchType,
            SortBy = request.SortBy,
            JournalId = request.JournalId,
            TopicId = request.TopicId,
            YearFrom = request.YearFrom,
            YearTo = request.YearTo,
            MinCitations = request.MinCitations,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
