using ScholarTrend.Application.DTOs.Journals;
using ScholarTrend.Application.DTOs.Trends;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Mappings;

namespace ScholarTrend.Application.Services;

public class JournalService : IJournalService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITrendService _trendService;

    public JournalService(IUnitOfWork unitOfWork, ITrendService trendService)
    {
        _unitOfWork = unitOfWork;
        _trendService = trendService;
    }

    public async Task<IReadOnlyList<JournalListItemDto>> GetAllAsync()
    {
        var journals = await _unitOfWork.Journals.GetAllAsync();
        var result = new List<JournalListItemDto>();

        foreach (var journal in journals)
        {
            var paperCount = await _unitOfWork.ResearchPapers.CountByJournalAsync(journal.Id);
            result.Add(new JournalListItemDto
            {
                Id = journal.Id,
                Name = journal.Name,
                Publisher = journal.Publisher,
                Issn = journal.Issn,
                ImpactFactor = journal.ImpactFactor,
                PaperCount = paperCount
            });
        }

        return result;
    }

    public async Task<JournalDetailDto> GetByIdAsync(int id)
    {
        var journal = await _unitOfWork.Journals.GetByIdAsync(id);
        if (journal == null)
        {
            throw new InvalidOperationException("Journal not found.");
        }

        var paperCount = await _unitOfWork.ResearchPapers.CountByJournalAsync(id);
        var recentPapers = await _unitOfWork.ResearchPapers.GetPapersByJournalAsync(id, limit: 5);
        var trendSeries = await _trendService.GetJournalTrendsAsync(new TrendFilterRequest { JournalId = id });

        return new JournalDetailDto
        {
            Id = journal.Id,
            Name = journal.Name,
            Publisher = journal.Publisher,
            Issn = journal.Issn,
            Website = journal.Website,
            ImpactFactor = journal.ImpactFactor,
            HIndex = journal.HIndex,
            PaperCount = paperCount,
            RecentPapers = recentPapers.Select(PaperMapper.ToListItem).ToList(),
            TrendChart = trendSeries.FirstOrDefault()?.DataPoints ?? []
        };
    }
}
