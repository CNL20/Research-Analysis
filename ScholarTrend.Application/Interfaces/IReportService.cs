using ScholarTrend.Application.DTOs.Reports;

namespace ScholarTrend.Application.Interfaces;

public interface IReportService
{
    Task<PublicationReportDto> GenerateReportAsync(ReportFilterRequest filter);
    byte[] ExportCsv(PublicationReportDto report);
}
