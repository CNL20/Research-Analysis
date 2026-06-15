using ScholarTrend.Application.DTOs.Authors;

namespace ScholarTrend.Application.Interfaces;

public interface IAuthorService
{
    Task<IReadOnlyList<AuthorListItemDto>> GetAllAsync();
    Task<AuthorDetailDto> GetByIdAsync(int id);
    Task<AuthorDetailDto> GetByNameAsync(string name);
}
