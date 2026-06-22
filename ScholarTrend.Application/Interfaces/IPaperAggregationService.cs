using ScholarTrend.Application.DTOs.Aggregation;

namespace ScholarTrend.Application.Interfaces;

public interface IPaperAggregationService
{
    Task<PaperAggregateResultDto> AggregateByDoiAsync(string doi);
    Task<PaperAggregateResultDto> AggregateByPaperIdAsync(int paperId);
}
