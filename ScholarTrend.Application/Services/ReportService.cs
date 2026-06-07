using System.Text;
using ScholarTrend.Application.DTOs.Reports;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;

namespace ScholarTrend.Application.Services;

public class ReportService : IReportService
{
    private readonly IStatisticsRepository _statistics;

    public ReportService(IStatisticsRepository statistics)
    {
        _statistics = statistics;
    }

    public async Task<PublicationReportDto> GenerateReportAsync(ReportFilterRequest filter)
    {
        var groupBy = filter.GroupBy.ToLowerInvariant();
        var yearFrom = filter.YearFrom ?? 2020;
        var yearTo = filter.YearTo ?? DateTime.UtcNow.Year;

        var items = groupBy switch
        {
            "keyword" => await _statistics.GetReportByKeywordAsync(yearFrom, yearTo),
            "topic" => await _statistics.GetReportByTopicAsync(yearFrom, yearTo),
            _ => await _statistics.GetReportByYearAsync(yearFrom, yearTo)
        };

        return new PublicationReportDto
        {
            GroupBy = groupBy,
            YearFrom = yearFrom,
            YearTo = yearTo,
            TotalPapers = items.Sum(i => i.PaperCount),
            Items = items.ToList(),
            GeneratedAt = DateTime.UtcNow
        };
    }

    public byte[] ExportCsv(PublicationReportDto report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"ScholarTrend Publication Report - GroupBy: {report.GroupBy}");
        sb.AppendLine($"Period: {report.YearFrom} - {report.YearTo}");
        sb.AppendLine($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("Key,PaperCount,TotalCitations");

        foreach (var item in report.Items)
        {
            sb.AppendLine($"\"{item.Key.Replace("\"", "\"\"")}\",{item.PaperCount},{item.TotalCitations}");
        }

        sb.AppendLine();
        sb.AppendLine($"Total Papers,{report.TotalPapers}");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
