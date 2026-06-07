using ScholarTrend.Application.DTOs.Journals;

namespace ScholarTrend.Application.Interfaces;

public interface IJournalService
{
    Task<IReadOnlyList<JournalListItemDto>> GetAllAsync();
    Task<JournalDetailDto> GetByIdAsync(int id);
}
