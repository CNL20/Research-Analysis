using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.DTOs.Papers;
using ScholarTrend.Application.Interfaces;

namespace ScholarTrend.Application.Interfaces;

public interface IPaperService
{
    Task<PagedResult<PaperListItemDto>> SearchAsync(PaperSearchRequest request, string userId);
    Task<PaperDetailDto> GetByIdAsync(int id, string userId);
    Task<PagedResult<PaperListItemDto>> GetByTopicAsync(int topicId, int page, int pageSize);
    Task<PagedResult<PaperListItemDto>> GetByJournalAsync(int journalId, int page, int pageSize);
    Task<IReadOnlyList<SearchHistoryDto>> GetSearchHistoryAsync(string userId, int limit = 20);
    Task RecordViewAsync(int id);
}
