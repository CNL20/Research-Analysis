using ScholarTrend.Application.Interfaces.External;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface IPaperImportRepository
{
    Task<ResearchPaperImportResult> ImportAsync(ExternalPaperDto external, int? journalId);
}

public class ResearchPaperImportResult
{
    public int PaperId { get; set; }
    public bool IsNew { get; set; }
}
