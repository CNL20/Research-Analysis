using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.GapAnalysis;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Services;

public class CoverageReportService : ICoverageReportService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CoverageReportService> _logger;

    public CoverageReportService(IUnitOfWork unitOfWork, ILogger<CoverageReportService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<CoverageReportDto> GenerateReportAsync(int topicId)
    {
        var topic = await _unitOfWork.Topics.GetByIdAsync(topicId);
        if (topic == null)
            throw new ArgumentException($"Topic {topicId} not found");

        var papers = (await _unitOfWork.ResearchPapers.GetPapersByTopicAsync(topicId)).ToList();
        var qualities = await _unitOfWork.PaperQualities.GetByTopicIdAsync(topicId);
        var analyses = await _unitOfWork.PaperAnalyses.GetByTopicIdAsync(topicId);

        var qualityDict = qualities.ToDictionary(q => q.PaperId);
        var analyzedPapers = analyses.Select(a => a.PaperId).ToHashSet();

        int fullText = 0, abstractOnly = 0, metadata = 0, ignored = 0;

        foreach (var paper in papers)
        {
            if (qualityDict.TryGetValue(paper.Id, out var q))
            {
                switch (q.AnalysisLevel)
                {
                    case AnalysisLevels.FullText:
                        fullText++;
                        break;
                    case AnalysisLevels.Abstract:
                        abstractOnly++;
                        break;
                    default:
                        metadata++;
                        break;
                }
            }
            else
            {
                ignored++;
            }
        }

        var total = papers.Count;
        var analyzed = fullText + abstractOnly;
        var coveragePercent = total > 0 ? (analyzed * 100.0 / total) : 0;
        var abstractPercent = total > 0 ? (abstractOnly * 100.0 / total) : 0;
        var fullTextPercent = total > 0 ? (fullText * 100.0 / total) : 0;

        var report = new CoverageReport
        {
            TopicId = topicId,
            TotalPapers = total,
            PdfAnalyzedPapers = fullText,
            AbstractAnalyzedPapers = abstractOnly,
            MetadataOnlyPapers = metadata,
            IgnoredPapers = ignored,
            CoveragePercentage = coveragePercent,
            AbstractCoveragePercentage = abstractPercent,
            FullTextCoveragePercentage = fullTextPercent,
            GeneratedAt = DateTime.UtcNow
        };

        var existing = await _unitOfWork.CoverageReports.GetLatestByTopicIdAsync(topicId);
        if (existing != null)
        {
            existing.TotalPapers = report.TotalPapers;
            existing.PdfAnalyzedPapers = report.PdfAnalyzedPapers;
            existing.AbstractAnalyzedPapers = report.AbstractAnalyzedPapers;
            existing.MetadataOnlyPapers = report.MetadataOnlyPapers;
            existing.IgnoredPapers = report.IgnoredPapers;
            existing.CoveragePercentage = report.CoveragePercentage;
            existing.AbstractCoveragePercentage = report.AbstractCoveragePercentage;
            existing.FullTextCoveragePercentage = report.FullTextCoveragePercentage;
            existing.GeneratedAt = report.GeneratedAt;
            await _unitOfWork.SaveChangesAsync();
        }
        else
        {
            await _unitOfWork.CoverageReports.AddAsync(report);
            await _unitOfWork.SaveChangesAsync();
        }

        return MapToDto(topic.TopicName, report);
    }

    public async Task<CoverageReportDto?> GetLatestReportAsync(int topicId)
    {
        var report = await _unitOfWork.CoverageReports.GetLatestByTopicIdAsync(topicId);
        if (report == null) return null;

        var topic = await _unitOfWork.Topics.GetByIdAsync(topicId);
        return MapToDto(topic?.TopicName ?? "", report);
    }

    public async Task<PaperQualityReportDto> GetQualityReportAsync(int topicId)
    {
        var topic = await _unitOfWork.Topics.GetByIdAsync(topicId);
        var qualities = await _unitOfWork.PaperQualities.GetByTopicIdAsync(topicId);

        var report = new PaperQualityReportDto
        {
            TopicId = topicId,
            TopicName = topic?.TopicName ?? "",
            TotalPapers = qualities.Count,
            GradeACount = qualities.Count(q => q.QualityGrade == QualityGrade.A),
            GradeBCount = qualities.Count(q => q.QualityGrade == QualityGrade.B),
            GradeCCount = qualities.Count(q => q.QualityGrade == QualityGrade.C),
            GradeDCount = qualities.Count(q => q.QualityGrade == QualityGrade.D),
            GradeFCount = qualities.Count(q => q.QualityGrade == QualityGrade.F),
            AverageQualityScore = qualities.Any() ? qualities.Average(q => q.QualityScore) : 0,
            AnalysisLevelBreakdown = new Dictionary<string, int>
            {
                ["FullText"] = qualities.Count(q => q.AnalysisLevel == AnalysisLevels.FullText),
                ["Abstract"] = qualities.Count(q => q.AnalysisLevel == AnalysisLevels.Abstract),
                ["Metadata"] = qualities.Count(q => q.AnalysisLevel == AnalysisLevels.Metadata)
            }
        };

        return report;
    }

    private CoverageReportDto MapToDto(string topicName, CoverageReport report)
    {
        return new CoverageReportDto
        {
            TopicId = report.TopicId,
            TopicName = topicName,
            TotalPapers = report.TotalPapers,
            PdfAnalyzedPapers = report.PdfAnalyzedPapers,
            AbstractAnalyzedPapers = report.AbstractAnalyzedPapers,
            MetadataOnlyPapers = report.MetadataOnlyPapers,
            IgnoredPapers = report.IgnoredPapers,
            CoveragePercentage = report.CoveragePercentage,
            AbstractCoveragePercentage = report.AbstractCoveragePercentage,
            FullTextCoveragePercentage = report.FullTextCoveragePercentage,
            GeneratedAt = report.GeneratedAt
        };
    }
}
