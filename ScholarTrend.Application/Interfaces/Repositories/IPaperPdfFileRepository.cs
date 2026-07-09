using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface IPaperPdfFileRepository
{
    Task<PaperPdfFile?> GetByIdAsync(int id);
    Task<PaperPdfFile?> GetByResearchPaperIdAsync(int researchPaperId);
    Task<List<PaperPdfFile>> GetByStatusAsync(string status, int take = 100);
    Task AddAsync(PaperPdfFile entity);
    void Update(PaperPdfFile entity);
    Task SaveChangesAsync();
    Task<List<PaperPdfFile>> GetStuckAsync(IEnumerable<string> statuses, int take = 100);
}
