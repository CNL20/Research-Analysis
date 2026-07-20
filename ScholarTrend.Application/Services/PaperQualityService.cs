using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.GapAnalysis;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Services;

public class PaperQualityService : IPaperQualityService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PaperQualityService> _logger;

    public PaperQualityService(IUnitOfWork unitOfWork, ILogger<PaperQualityService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PaperQuality> AssessPaperAsync(int paperId)
    {
        var paper = await _unitOfWork.ResearchPapers.GetByIdAsync(paperId);
        if (paper == null)
            throw new ArgumentException($"Paper {paperId} not found");

        var quality = new PaperQuality
        {
            PaperId = paperId,
            HasPdf = !string.IsNullOrWhiteSpace(paper.PdfUrl),
            HasAbstract = !string.IsNullOrWhiteSpace(paper.Abstract),
            AbstractLength = paper.Abstract?.Length ?? 0,
            AuthorCount = await GetAuthorCountAsync(paperId),
            HasDoi = !string.IsNullOrWhiteSpace(paper.Doi),
            HasKeywords = await HasKeywordsAsync(paperId),
            HasJournal = paper.JournalId.HasValue,
            CitationCount = paper.CitationCount ?? 0,
            AssessedAt = DateTime.UtcNow
        };

        quality.QualityScore = CalculateQualityScore(quality);
        quality.QualityGrade = GetQualityGrade(quality.QualityScore);
        quality.AnalysisLevel = DetermineAnalysisLevel(quality);

        var existing = await _unitOfWork.PaperQualities.GetByPaperIdAsync(paperId);
        if (existing != null)
        {
            existing.HasPdf = quality.HasPdf;
            existing.HasAbstract = quality.HasAbstract;
            existing.AbstractLength = quality.AbstractLength;
            existing.AuthorCount = quality.AuthorCount;
            existing.HasDoi = quality.HasDoi;
            existing.HasKeywords = quality.HasKeywords;
            existing.HasJournal = quality.HasJournal;
            existing.CitationCount = quality.CitationCount;
            existing.QualityScore = quality.QualityScore;
            existing.QualityGrade = quality.QualityGrade;
            existing.AnalysisLevel = quality.AnalysisLevel;
            existing.AssessedAt = quality.AssessedAt;
            _unitOfWork.PaperQualities.Update(existing);
            await _unitOfWork.SaveChangesAsync();
            return existing;
        }

        await _unitOfWork.PaperQualities.AddAsync(quality);
        await _unitOfWork.SaveChangesAsync();
        return quality;
    }

    public async Task<PaperQuality> GetOrAssessAsync(int paperId)
    {
        var existing = await _unitOfWork.PaperQualities.GetByPaperIdAsync(paperId);
        if (existing != null)
            return existing;
        return await AssessPaperAsync(paperId);
    }

    public async Task<Dictionary<string, int>> GetCoverageReportAsync(int topicId)
    {
        var papers = await _unitOfWork.ResearchPapers.GetPapersByTopicAsync(topicId);
        var qualities = await _unitOfWork.PaperQualities.GetByTopicIdAsync(topicId);
        var paperIds = papers.Select(p => p.Id).ToHashSet();
        
        var qualityDict = qualities.ToDictionary(q => q.PaperId);

        int fullText = 0, abstractOnly = 0, metadata = 0, ignored = 0;

        foreach (var paperId in paperIds)
        {
            if (qualityDict.TryGetValue(paperId, out var q))
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

        return new Dictionary<string, int>
        {
            ["TotalPapers"] = papers.Count(),
            ["FullTextAnalyzed"] = fullText,
            ["AbstractAnalyzed"] = abstractOnly,
            ["MetadataOnly"] = metadata,
            ["Ignored"] = ignored,
            ["CoveragePercentage"] = (int)((fullText + abstractOnly) * 100.0 / Math.Max(papers.Count(), 1))
        };
    }

    private async Task<int> GetAuthorCountAsync(int paperId)
    {
        var paper = await _unitOfWork.ResearchPapers.GetByIdAsync(paperId);
        return paper?.PaperAuthors?.Count ?? 0;
    }

    private async Task<bool> HasKeywordsAsync(int paperId)
    {
        var paper = await _unitOfWork.ResearchPapers.GetByIdAsync(paperId);
        return paper?.PaperKeywords?.Any() ?? false;
    }

    private int CalculateQualityScore(PaperQuality q)
    {
        int score = 0;
        
        if (q.HasPdf) score += 25;
        if (q.HasAbstract) score += 25;
        if (q.AbstractLength > 200) score += 15;
        if (q.HasDoi) score += 10;
        if (q.HasKeywords) score += 10;
        if (q.HasJournal) score += 10;
        if (q.AuthorCount > 0) score += 5;
        
        return Math.Min(score, 100);
    }

    private string GetQualityGrade(int score)
    {
        return score switch
        {
            >= 80 => QualityGrade.A,
            >= 60 => QualityGrade.B,
            >= 40 => QualityGrade.C,
            >= 20 => QualityGrade.D,
            _ => QualityGrade.F
        };
    }

    private string DetermineAnalysisLevel(PaperQuality q)
    {
        if (q.HasPdf && q.HasFullText) return AnalysisLevels.FullText;
        if (q.HasAbstract) return AnalysisLevels.Abstract;
        return AnalysisLevels.Metadata;
    }
}
