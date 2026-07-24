using ScholarTrend.Application.DTOs.Reports;

namespace ScholarTrend.Application.Interfaces.Repositories;

public interface IStatisticsRepository
{
    Task<int> CountPapersAsync(int? yearFrom = null, int? yearTo = null);
    Task<int> CountKeywordsAsync();
    Task<int> CountTopicsAsync();
    Task<int> CountJournalsAsync();
    Task<int> CountAuthorsAsync();
    Task<int> CountBookmarksAsync();
    Task<int> CountFollowsAsync();
    Task<int> CountActiveUsersAsync();
    Task<IReadOnlyList<ReportGroupItemDto>> GetReportByYearAsync(int? yearFrom, int? yearTo);
    Task<IReadOnlyList<ReportGroupItemDto>> GetReportByKeywordAsync(int? yearFrom, int? yearTo);
    Task<IReadOnlyList<ReportGroupItemDto>> GetReportByTopicAsync(int? yearFrom, int? yearTo);
    Task<IReadOnlyList<ReportGroupItemDto>> GetReportByJournalAsync(int? yearFrom, int? yearTo);
    Task<IReadOnlyDictionary<int, double>> GetKeywordReliabilityAsync(IEnumerable<int> keywordIds, int? yearFrom, int? yearTo);
    Task<IReadOnlyDictionary<int, double>> GetTopicReliabilityAsync(IEnumerable<int> topicIds, int? yearFrom, int? yearTo);
    Task<IReadOnlyDictionary<int, double>> GetJournalReliabilityAsync(IEnumerable<int> journalIds, int? yearFrom, int? yearTo);
}
