using System.Text.Json;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.GapAnalysis;
using ScholarTrend.Application.DTOs.TopicInsights;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Services;

public class PaperAnalysisService : IPaperAnalysisService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAiExtractionService _aiExtractionService;
    private readonly ILogger<PaperAnalysisService> _logger;
    private const int BatchSize = 10;

    public PaperAnalysisService(
        IUnitOfWork unitOfWork,
        IAiExtractionService aiExtractionService,
        ILogger<PaperAnalysisService> logger)
    {
        _unitOfWork = unitOfWork;
        _aiExtractionService = aiExtractionService;
        _logger = logger;
    }

    public async Task<PaperAnalysis?> AnalyzePaperAsync(int paperId, CancellationToken ct = default)
    {
        var paper = await _unitOfWork.ResearchPapers.GetByIdAsync(paperId);
        if (paper == null)
        {
            _logger.LogWarning("Paper {PaperId} not found for analysis", paperId);
            return null;
        }

        var quality = await _unitOfWork.PaperQualities.GetByPaperIdAsync(paperId);
        if (quality == null)
        {
            quality = new PaperQuality { PaperId = paperId };
            quality.HasPdf = !string.IsNullOrWhiteSpace(paper.PdfUrl);
            quality.HasAbstract = !string.IsNullOrWhiteSpace(paper.Abstract);
            await _unitOfWork.PaperQualities.AddAsync(quality);
            await _unitOfWork.Context.SaveChangesAsync(ct);
        }

        var extraction = await _aiExtractionService.ExtractFromAbstractAsync(paper.Abstract ?? "", ct);
        if (extraction == null)
        {
            _logger.LogWarning("Failed to extract from paper {PaperId}", paperId);
            return null;
        }

        var analysis = await _unitOfWork.PaperAnalyses.GetByPaperIdAsync(paperId) 
            ?? new PaperAnalysis { PaperId = paperId };

        analysis.ResearchProblem = extraction.ResearchProblem;
        analysis.Method = extraction.Methods.FirstOrDefault();
        analysis.Dataset = extraction.Datasets.FirstOrDefault();
        analysis.Metric = extraction.Metric;
        analysis.Contribution = extraction.Contribution;
        analysis.MethodsJson = JsonSerializer.Serialize(extraction.Methods);
        analysis.DatasetsJson = JsonSerializer.Serialize(extraction.Datasets);
        analysis.LimitationsJson = JsonSerializer.Serialize(extraction.Limitations);
        analysis.FutureWorkJson = JsonSerializer.Serialize(extraction.FutureWork);
        analysis.DiscussionsJson = JsonSerializer.Serialize(extraction.Discussions);
        analysis.ConclusionsJson = JsonSerializer.Serialize(extraction.Conclusions);
        analysis.Confidence = CalculateConfidence(extraction);
        analysis.AnalysisLevel = quality.HasPdf ? AnalysisLevels.Abstract : AnalysisLevels.Abstract;
        analysis.AnalysisSource = "Groq";
        analysis.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.PaperAnalyses.UpsertAsync(analysis);
        await _unitOfWork.Context.SaveChangesAsync(ct);

        _logger.LogInformation("Analyzed paper {PaperId} with confidence {Confidence}", paperId, analysis.Confidence);
        return analysis;
    }

    public async Task<PaperAnalysis?> GetAnalysisAsync(int paperId)
    {
        return await _unitOfWork.PaperAnalyses.GetByPaperIdAsync(paperId);
    }

    public async Task<int> AnalyzePapersByTopicAsync(int topicId, CancellationToken ct = default)
    {
        var papers = await _unitOfWork.ResearchPapers.GetPapersByTopicAsync(topicId);
        var analyzed = 0;

        foreach (var paper in papers.Take(BatchSize))
        {
            var existing = await _unitOfWork.PaperAnalyses.GetByPaperIdAsync(paper.Id);
            if (existing == null)
            {
                await AnalyzePaperAsync(paper.Id, ct);
                analyzed++;
                await Task.Delay(4000, ct);
            }
        }

        return analyzed;
    }

    public async Task<GapAnalysisResultDto> GetAnalysisResultAsync(int topicId)
    {
        var papers = (await _unitOfWork.ResearchPapers.GetPapersByTopicAsync(topicId)).ToList();
        var analyses = await _unitOfWork.PaperAnalyses.GetByTopicIdAsync(topicId);
        var recentAnalyses = await _unitOfWork.PaperAnalyses.GetByTopicIdWithLimitAsync(topicId, 20);

        return new GapAnalysisResultDto
        {
            TopicId = topicId,
            TotalPapers = papers.Count,
            AnalyzedPapers = analyses.Count,
            PendingPapers = papers.Count - analyses.Count,
            FailedPapers = 0,
            AnalysisProgress = papers.Count > 0 ? (analyses.Count * 100.0 / papers.Count) : 0,
            RecentAnalyses = recentAnalyses.Select(MapToDto).ToList()
        };
    }

    private int CalculateConfidence(AiPaperExtractionDto extraction)
    {
        int score = 50;
        if (extraction.Methods.Any()) score += 10;
        if (extraction.Datasets.Any()) score += 10;
        if (extraction.Limitations.Any()) score += 10;
        if (extraction.FutureWork.Any()) score += 10;
        if (extraction.ResearchProblem != null) score += 5;
        if (extraction.Metric != null) score += 5;
        return Math.Min(score, 100);
    }

    private PaperAnalysisDto MapToDto(PaperAnalysis a)
    {
        return new PaperAnalysisDto
        {
            PaperId = a.PaperId,
            Title = a.Paper?.Title ?? "",
            Year = a.Paper?.PublicationYear ?? 0,
            ResearchProblem = a.ResearchProblem,
            Method = a.Method,
            Dataset = a.Dataset,
            Metric = a.Metric,
            Contribution = a.Contribution,
            Methods = DeserializeList(a.MethodsJson),
            Datasets = DeserializeList(a.DatasetsJson),
            Limitations = DeserializeList(a.LimitationsJson),
            FutureWork = DeserializeList(a.FutureWorkJson),
            Discussions = DeserializeList(a.DiscussionsJson),
            Conclusions = DeserializeList(a.ConclusionsJson),
            AnalysisLevel = a.AnalysisLevel,
            Confidence = a.Confidence,
            AnalyzedAt = a.CreatedAt
        };
    }

    private List<string> DeserializeList(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch { return []; }
    }
}
