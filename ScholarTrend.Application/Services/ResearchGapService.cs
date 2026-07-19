using System.Text.Json;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.GapAnalysis;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Services;

public class ResearchGapService : IResearchGapService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAiExtractionService _aiExtractionService;
    private readonly IPatternMiningService _patternMiningService;
    private readonly ICoverageReportService _coverageReportService;
    private readonly ILogger<ResearchGapService> _logger;

    public ResearchGapService(
        IUnitOfWork unitOfWork,
        IAiExtractionService aiExtractionService,
        IPatternMiningService patternMiningService,
        ICoverageReportService coverageReportService,
        ILogger<ResearchGapService> logger)
    {
        _unitOfWork = unitOfWork;
        _aiExtractionService = aiExtractionService;
        _patternMiningService = patternMiningService;
        _coverageReportService = coverageReportService;
        _logger = logger;
    }

    public async Task<ResearchGapReportDto> GenerateGapReportAsync(int topicId, CancellationToken ct = default)
    {
        var topic = await _unitOfWork.Topics.GetByIdAsync(topicId);
        if (topic == null)
            throw new ArgumentException($"Topic {topicId} not found");

        _logger.LogInformation("Generating research gap report for topic {TopicId} ({TopicName})", topicId, topic.TopicName);

        var patterns = await _patternMiningService.MinePatternsAsync(topicId, ct);
        var timeline = await BuildGapTimelineAsync(topicId, ct);
        var analyses = await GetPaperAnalysesAsync(topicId);

        var generatedGaps = await _aiExtractionService.GenerateResearchGapsAsync(
            topic.TopicName,
            patterns,
            timeline,
            analyses,
            ct);

        var savedGaps = await SaveGapsWithEvidenceAsync(topicId, generatedGaps, analyses, ct);
        var coverage = await _coverageReportService.GenerateReportAsync(topicId);

        return new ResearchGapReportDto
        {
            TopicId = topicId,
            TopicName = topic.TopicName,
            Gaps = savedGaps,
            Patterns = patterns,
            Timeline = timeline,
            Coverage = coverage,
            GeneratedAt = DateTime.UtcNow
        };
    }

    public async Task<List<ResearchGapDto>> GetGapsAsync(int topicId)
    {
        var gaps = await _unitOfWork.ResearchGaps.GetByTopicIdAsync(topicId);
        return gaps.Select(MapToDto).ToList();
    }

    public async Task<ResearchGapDetailDto?> GetGapDetailAsync(int gapId)
    {
        var gap = await _unitOfWork.ResearchGaps.GetByIdWithEvidencesAsync(gapId);
        if (gap == null) return null;

        var dto = new ResearchGapDetailDto
        {
            Id = gap.Id,
            Title = gap.Title,
            Description = gap.Description,
            GapType = gap.GapType,
            SuggestedDirection = gap.SuggestedDirection,
            EvidenceCount = gap.EvidenceCount,
            Confidence = gap.Confidence,
            ConfidenceLevel = gap.ConfidenceLevel,
            Evidences = gap.Evidences.Select(e => new ResearchGapEvidenceDto
            {
                Id = e.Id,
                PaperId = e.PaperId,
                PaperTitle = e.Paper?.Title ?? "",
                Authors = GetAuthorsString(e.Paper),
                Year = e.Paper?.PublicationYear ?? 0,
                EvidenceSentence = e.EvidenceSentence,
                EvidenceType = e.EvidenceType,
                SectionSource = e.SectionSource ?? "",
                Confidence = e.Confidence
            }).ToList()
        };

        return dto;
    }

    public async Task<List<ResearchGapEvidenceDto>> GetGapEvidencesAsync(int gapId)
    {
        var gap = await _unitOfWork.ResearchGaps.GetByIdWithEvidencesAsync(gapId);
        if (gap == null) return [];

        return gap.Evidences.Select(e => new ResearchGapEvidenceDto
        {
            Id = e.Id,
            PaperId = e.PaperId,
            PaperTitle = e.Paper?.Title ?? "",
            Authors = GetAuthorsString(e.Paper),
            Year = e.Paper?.PublicationYear ?? 0,
            EvidenceSentence = e.EvidenceSentence,
            EvidenceType = e.EvidenceType,
            SectionSource = e.SectionSource ?? "",
            Confidence = e.Confidence
        }).ToList();
    }

    private async Task<GapTimelineDto> BuildGapTimelineAsync(int topicId, CancellationToken ct)
    {
        var topic = await _unitOfWork.Topics.GetByIdAsync(topicId);
        var timelines = await _unitOfWork.GapTimelines.GetByTopicIdAsync(topicId);

        return new GapTimelineDto
        {
            TopicId = topicId,
            TopicName = topic?.TopicName ?? "",
            Timeline = timelines.Select(t => new GapTimelineEntryDto
            {
                Year = t.Year,
                GapType = t.GapType,
                GapTitle = t.GapTitle,
                PaperCount = t.PaperCount,
                IsResolved = t.IsResolved,
                Trend = t.Trend,
                GrowthRate = 0
            }).ToList()
        };
    }

    private async Task<List<PaperAnalysisDto>> GetPaperAnalysesAsync(int topicId)
    {
        var analyses = await _unitOfWork.PaperAnalyses.GetByTopicIdAsync(topicId);
        return analyses.Select(a => new PaperAnalysisDto
        {
            PaperId = a.PaperId,
            Title = a.Paper?.Title ?? "",
            Year = a.Paper?.PublicationYear ?? 0,
            ResearchProblem = a.ResearchProblem,
            Method = a.Method,
            Dataset = a.Dataset,
            Limitations = DeserializeList(a.LimitationsJson),
            FutureWork = DeserializeList(a.FutureWorkJson),
            Confidence = a.Confidence
        }).ToList();
    }

    private async Task<List<ResearchGapDto>> SaveGapsWithEvidenceAsync(
        int topicId,
        List<ResearchGapDto> generatedGaps,
        List<PaperAnalysisDto> analyses,
        CancellationToken ct)
    {
        var savedGaps = new List<ResearchGapDto>();

        foreach (var gap in generatedGaps)
        {
            var researchGap = new ResearchGap
            {
                TopicId = topicId,
                Title = gap.Title,
                Description = gap.Description,
                GapType = gap.GapType,
                SuggestedDirection = gap.SuggestedDirection,
                EvidenceCount = gap.EvidenceCount,
                Confidence = gap.Confidence,
                ConfidenceLevel = ConfidenceLevels.GetLevel(gap.Confidence),
                IsValidated = false,
                GeneratedAt = DateTime.UtcNow
            };

            await _unitOfWork.ResearchGaps.AddAsync(researchGap);
            await _unitOfWork.Context.SaveChangesAsync(ct);

            var evidenceCount = Math.Min(gap.EvidenceCount, analyses.Count);
            for (int i = 0; i < evidenceCount && i < analyses.Count; i++)
            {
                var analysis = analyses[i];
                var evidence = new ResearchGapEvidence
                {
                    ResearchGapId = researchGap.Id,
                    PaperId = analysis.PaperId,
                    EvidenceSentence = GetRelevantSentence(analysis, gap.GapType),
                    EvidenceType = DetermineEvidenceType(gap.GapType),
                    Confidence = analysis.Confidence,
                    IsValidated = false,
                    ValidationStatus = ValidationStatuses.Pending
                };
                
                await _unitOfWork.Context.AddAsync(evidence);
            }

            researchGap.EvidenceCount = evidenceCount;
            await _unitOfWork.Context.SaveChangesAsync(ct);

            savedGaps.Add(MapToDto(researchGap));
        }

        return savedGaps;
    }

    private string GetRelevantSentence(PaperAnalysisDto analysis, string gapType)
    {
        return gapType.ToLowerInvariant() switch
        {
            "dataset gap" => analysis.Dataset ?? "Dataset information not available",
            "method gap" => analysis.Method ?? "Method information not available",
            "evaluation gap" => "Evaluation methodology needs improvement",
            "application gap" => "Application scope needs expansion",
            "geographic gap" => "Geographic coverage needs expansion",
            "temporal gap" => "Temporal scope needs expansion",
            "contradiction gap" => "Contradictory findings need resolution",
            _ => analysis.ResearchProblem ?? "Research gap identified"
        };
    }

    private string DetermineEvidenceType(string gapType)
    {
        return gapType.ToLowerInvariant() switch
        {
            "dataset gap" => EvidenceTypes.Discussion,
            "method gap" => EvidenceTypes.Discussion,
            "evaluation gap" => EvidenceTypes.Conclusion,
            "application gap" => EvidenceTypes.FutureWork,
            "geographic gap" => EvidenceTypes.Discussion,
            "temporal gap" => EvidenceTypes.Limitation,
            "contradiction gap" => EvidenceTypes.Discussion,
            _ => EvidenceTypes.Discussion
        };
    }

    private ResearchGapDto MapToDto(ResearchGap gap)
    {
        return new ResearchGapDto
        {
            Id = gap.Id,
            Title = gap.Title,
            Description = gap.Description,
            GapType = gap.GapType,
            SuggestedDirection = gap.SuggestedDirection,
            EvidenceCount = gap.EvidenceCount,
            Confidence = gap.Confidence,
            ConfidenceLevel = gap.ConfidenceLevel
        };
    }

    private string GetAuthorsString(ResearchPaper? paper)
    {
        if (paper?.PaperAuthors == null || !paper.PaperAuthors.Any())
            return "Unknown";
        return string.Join(", ", paper.PaperAuthors.OrderBy(pa => pa.AuthorOrder).Take(3).Select(pa => pa.Author?.Name ?? "Unknown"));
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
