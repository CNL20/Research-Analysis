using ScholarTrend.Application.DTOs.Authors;
using ScholarTrend.Application.DTOs.Common;

namespace ScholarTrend.Application.Interfaces;

public interface IAuthorService
{
    Task<PagedResult<AuthorListItemDto>> GetPagedAsync(string? keyword, int page, int pageSize);
    Task<AuthorDetailDto> GetByIdAsync(int id);
    Task<AuthorDetailDto> GetByNameAsync(string name);
}
