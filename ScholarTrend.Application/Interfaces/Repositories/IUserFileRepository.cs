using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface IUserFileRepository
{
    Task<UserFile?> GetByIdAsync(int id);
    Task<(IReadOnlyList<UserFile> Items, int TotalCount)> GetUserFilesAsync(
        string userId,
        string? category,
        int page,
        int pageSize);
    Task<int> CountActiveByUserAsync(string userId);
    Task<IReadOnlyList<UserFile>> GetActiveAvatarsByUserAsync(string userId);
    Task<List<UserFile>> GetByPaperIdAsync(int paperId);
    Task AddAsync(UserFile file);
    Task UpdateAsync(UserFile file);
}
