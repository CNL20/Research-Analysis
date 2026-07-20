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

        // Load supporting patterns for this topic (so the UI can show what patterns back this gap)
        dto.SupportingPatterns = await BuildSupportingPatternsAsync(gap);

        // Load top related papers (papers that evidence this gap, plus top papers in the topic)
        dto.TopRelatedPapers = await BuildTopRelatedPapersAsync(gap);

        // Load trend info if a timeline entry exists for this gap type
        dto.TrendInfo = await BuildTrendInfoAsync(gap);

        return dto;
    }

    private async Task<PatternMiningResultDto> BuildSupportingPatternsAsync(ResearchGap gap)
    {
        var patterns = new PatternMiningResultDto
        {
            TopicId = gap.TopicId,
            TopicName = "",
            Methods = [],
            Datasets = [],
            Limitations = [],
            MinedAt = DateTime.UtcNow
        };

        var topic = await _unitOfWork.Topics.GetByIdAsync(gap.TopicId);
        patterns.TopicName = topic?.TopicName ?? "";

        var gapType = (gap.GapType ?? string.Empty).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(gapType)) return patterns;

        // Pick patterns relevant to this gap type.
        // For example: a "Dataset Gap" should show top datasets + limitations mentioning data.
        var datasetPatterns = await _unitOfWork.Patterns.GetDatasetPatternsAsync(gap.TopicId);
        var methodPatterns = await _unitOfWork.Patterns.GetMethodPatternsAsync(gap.TopicId);
        var limitationPatterns = await _unitOfWork.Patterns.GetLimitationPatternsAsync(gap.TopicId);

        if (gapType.Contains("dataset"))
        {
            patterns.Datasets = datasetPatterns
                .GroupBy(p => p.DatasetName)
                .Select(g => new DatasetPatternDto
                {
                    DatasetName = g.Key,
                    PaperCount = g.Sum(p => p.PaperCount),
                    Year = g.Max(p => p.Year),
                    GrowthRate = 0,
                    Trend = "stable"
                })
                .OrderByDescending(d => d.PaperCount)
                .Take(5)
                .ToList();
        }
        else if (gapType.Contains("method"))
        {
            patterns.Methods = methodPatterns
                .GroupBy(p => p.MethodName)
                .Select(g => new MethodPatternDto
                {
                    MethodName = g.Key,
                    PaperCount = g.Sum(p => p.PaperCount),
                    Year = g.Max(p => p.Year),
                    GrowthRate = 0,
                    Trend = "stable"
                })
                .OrderByDescending(m => m.PaperCount)
                .Take(5)
                .ToList();
        }
        else if (gapType.Contains("evaluation"))
        {
            patterns.Methods = methodPatterns
                .GroupBy(p => p.MethodName)
                .Select(g => new MethodPatternDto
                {
                    MethodName = g.Key,
                    PaperCount = g.Sum(p => p.PaperCount),
                    Year = g.Max(p => p.Year),
                    GrowthRate = 0,
                    Trend = "stable"
                })
                .OrderByDescending(m => m.PaperCount)
                .Take(3)
                .ToList();
            patterns.Datasets = datasetPatterns
                .GroupBy(p => p.DatasetName)
                .Select(g => new DatasetPatternDto
                {
                    DatasetName = g.Key,
                    PaperCount = g.Sum(p => p.PaperCount),
                    Year = g.Max(p => p.Year),
                    GrowthRate = 0,
                    Trend = "stable"
                })
                .OrderByDescending(d => d.PaperCount)
                .Take(3)
                .ToList();
        }
        else
        {
            // Default: provide all three so the UI can show something useful
            patterns.Methods = methodPatterns
                .GroupBy(p => p.MethodName)
                .Select(g => new MethodPatternDto
                {
                    MethodName = g.Key,
                    PaperCount = g.Sum(p => p.PaperCount),
                    Year = g.Max(p => p.Year),
                    GrowthRate = 0,
                    Trend = "stable"
                })
                .OrderByDescending(m => m.PaperCount)
                .Take(3)
                .ToList();
            patterns.Datasets = datasetPatterns
                .GroupBy(p => p.DatasetName)
                .Select(g => new DatasetPatternDto
                {
                    DatasetName = g.Key,
                    PaperCount = g.Sum(p => p.PaperCount),
                    Year = g.Max(p => p.Year),
                    GrowthRate = 0,
                    Trend = "stable"
                })
                .OrderByDescending(d => d.PaperCount)
                .Take(3)
                .ToList();
        }

        patterns.Limitations = limitationPatterns
            .GroupBy(p => p.LimitationText)
            .Select(g => new LimitationPatternDto
            {
                LimitationText = g.Key,
                PaperCount = g.Sum(p => p.PaperCount),
                Year = g.Max(p => p.Year),
                GrowthRate = 0,
                Trend = "stable"
            })
            .OrderByDescending(l => l.PaperCount)
            .Take(3)
            .ToList();

        return patterns;
    }

    private async Task<List<RelatedPaperDto>> BuildTopRelatedPapersAsync(ResearchGap gap)
    {
        // 1) Papers already linked as evidence (most relevant)
        var evidencePaperIds = gap.Evidences.Select(e => e.PaperId).Distinct().ToList();
        var evidencePapers = gap.Evidences
            .Where(e => e.Paper != null)
            .Select(e => new RelatedPaperDto
            {
                PaperId = e.PaperId,
                Title = e.Paper!.Title,
                Authors = GetAuthorsString(e.Paper),
                Year = e.Paper.PublicationYear ?? 0,
                CitationCount = e.Paper.CitationCount ?? 0,
                Contribution = e.EvidenceSentence
            })
            .ToList();

        // 2) Fill with top papers from the topic (by confidence) so the UI has more context
        var analyses = await _unitOfWork.PaperAnalyses.GetByTopicIdAsync(gap.TopicId);
        var topPapers = analyses
            .Where(a => a.Paper != null && !evidencePaperIds.Contains(a.PaperId))
            .OrderByDescending(a => a.Confidence)
            .Take(Math.Max(0, 5 - evidencePapers.Count))
            .Select(a => new RelatedPaperDto
            {
                PaperId = a.PaperId,
                Title = a.Paper!.Title,
                Authors = GetAuthorsString(a.Paper),
                Year = a.Paper.PublicationYear ?? 0,
                CitationCount = a.Paper.CitationCount ?? 0,
                Contribution = a.Contribution ?? ""
            })
            .ToList();

        return evidencePapers.Concat(topPapers).Take(5).ToList();
    }

    private async Task<GapTimelineEntryDto?> BuildTrendInfoAsync(ResearchGap gap)
    {
        // Try to find a matching timeline entry for this gap type
        var timelines = await _unitOfWork.GapTimelines.GetByTopicIdAsync(gap.TopicId);
        var match = timelines
            .Where(t => !string.IsNullOrWhiteSpace(gap.GapType)
                        && t.GapType.Equals(gap.GapType, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(t => t.Year)
            .FirstOrDefault();

        if (match != null)
        {
            return new GapTimelineEntryDto
            {
                Year = match.Year,
                GapType = match.GapType,
                GapTitle = match.GapTitle,
                PaperCount = match.PaperCount,
                IsResolved = match.IsResolved,
                Trend = match.Trend,
                GrowthRate = match.GrowthRate
            };
        }

        // No timeline yet: synthesize a default TrendInfo derived from patterns
        // so the UI never sees a null and can show "stable" / "emerging" status.
        var methodPatterns = await _unitOfWork.Patterns.GetMethodPatternsAsync(gap.TopicId);
        var mostRecentYear = methodPatterns.Any() ? methodPatterns.Max(p => p.Year) : DateTime.UtcNow.Year;
        var paperCount = methodPatterns.Where(p => p.Year == mostRecentYear).Sum(p => p.PaperCount);

        return new GapTimelineEntryDto
        {
            Year = mostRecentYear,
            GapType = gap.GapType,
            GapTitle = gap.Title,
            PaperCount = paperCount,
            IsResolved = false,
            Trend = GapTrends.Stable,
            GrowthRate = 0
        };
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

        if (!analyses.Any())
        {
            _logger.LogWarning("No paper analyses found for topic {TopicId}. Evidence linking skipped.", topicId);
            // Still save gaps with 0 evidences so they appear in the UI
            foreach (var gap in generatedGaps)
            {
                var rg = new ResearchGap
                {
                    TopicId = topicId,
                    Title = gap.Title,
                    Description = gap.Description,
                    GapType = gap.GapType,
                    SuggestedDirection = gap.SuggestedDirection,
                    EvidenceCount = 0,
                    Confidence = gap.Confidence,
                    ConfidenceLevel = ConfidenceLevels.GetLevel(gap.Confidence),
                    IsValidated = false,
                    GeneratedAt = DateTime.UtcNow
                };
                await _unitOfWork.ResearchGaps.AddAsync(rg);
                await _unitOfWork.Context.SaveChangesAsync(ct);
                savedGaps.Add(MapToDto(rg));
            }
            return savedGaps;
        }

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

            // Determine which analyses to link as evidence.
            // Priority 1: AI explicitly told us which paper IDs back this gap.
            // Priority 2: fall back to the gap.EvidenceCount top-scoring papers.
            // Priority 3: fall back to the top 3 papers so users always see at least minimal evidence.
            var selectedAnalyses = SelectEvidenceAnalyses(gap, analyses);

            var evidenceCount = selectedAnalyses.Count;
            foreach (var analysis in selectedAnalyses)
            {
                var evidence = new ResearchGapEvidence
                {
                    ResearchGapId = researchGap.Id,
                    PaperId = analysis.PaperId,
                    EvidenceSentence = GetRelevantSentence(analysis, gap),
                    EvidenceType = DetermineEvidenceType(gap),
                    SectionSource = DetermineSectionSource(gap),
                    Confidence = analysis.Confidence,
                    IsValidated = false,
                    ValidationStatus = ValidationStatuses.Pending
                };

                await _unitOfWork.Context.AddAsync(evidence);
            }

            researchGap.EvidenceCount = evidenceCount;
            await _unitOfWork.Context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Saved gap {GapId} ({Title}) with {EvidenceCount} evidences (gap_type={GapType})",
                researchGap.Id, researchGap.Title, evidenceCount, researchGap.GapType);

            savedGaps.Add(MapToDto(researchGap));
        }

        return savedGaps;
    }

    /// <summary>
    /// Select which analyses to link as evidence for a gap.
    /// Strategy: prefer AI-supplied paper IDs, then fall back to top papers
    /// by confidence, always guaranteeing at least a few evidences when analyses exist.
    /// </summary>
    private static List<PaperAnalysisDto> SelectEvidenceAnalyses(
        ResearchGapDto gap,
        List<PaperAnalysisDto> analyses)
    {
        // 1) AI supplied specific paper IDs
        if (gap.SupportingPaperIds != null && gap.SupportingPaperIds.Any())
        {
            var matched = analyses
                .Where(a => gap.SupportingPaperIds.Contains(a.PaperId))
                .OrderByDescending(a => a.Confidence)
                .ToList();

            if (matched.Any())
                return matched;
        }

        // 2) Use AI's evidence_count to take the top N analyses
        var requested = gap.EvidenceCount > 0 ? gap.EvidenceCount : 3;
        requested = Math.Min(requested, analyses.Count);

        // Prefer papers whose content matches the gap type (e.g., for "Dataset Gap",
        // prefer papers with dataset descriptions)
        var scored = analyses
            .Select(a => new
            {
                Analysis = a,
                Score = ScoreRelevance(a, gap.GapType) + (a.Confidence / 100.0)
            })
            .OrderByDescending(x => x.Score)
            .Take(requested)
            .Select(x => x.Analysis)
            .ToList();

        return scored;
    }

    private static double ScoreRelevance(PaperAnalysisDto analysis, string gapType)
    {
        if (string.IsNullOrWhiteSpace(gapType)) return 0;

        var gt = gapType.ToLowerInvariant();
        double score = 0;
        if (gt.Contains("dataset") && !string.IsNullOrWhiteSpace(analysis.Dataset)) score += 1.0;
        if (gt.Contains("method") && !string.IsNullOrWhiteSpace(analysis.Method)) score += 1.0;
        if (gt.Contains("evaluation") && !string.IsNullOrWhiteSpace(analysis.Metric)) score += 1.0;
        if (gt.Contains("application") && analysis.FutureWork.Any()) score += 1.0;
        if (gt.Contains("geographic") || gt.Contains("temporal") || gt.Contains("contradiction"))
            score += analysis.Limitations.Any() ? 1.0 : 0;
        return score;
    }

    private string GetRelevantSentence(PaperAnalysisDto analysis, ResearchGapDto gap)
    {
        var gapType = (gap.GapType ?? string.Empty).ToLowerInvariant();

        // Prefer limitations if the gap is about limitations/contradictions
        if (gapType.Contains("contradiction") || gapType.Contains("limitation"))
        {
            if (analysis.Limitations.Any())
                return analysis.Limitations.First();
        }

        // Prefer future work for "application" or directional gaps
        if (gapType.Contains("application") && analysis.FutureWork.Any())
            return analysis.FutureWork.First();

        // Prefer dataset for "dataset" gap
        if (gapType.Contains("dataset") && !string.IsNullOrWhiteSpace(analysis.Dataset))
            return $"Dataset used: {analysis.Dataset}";

        // Prefer method for "method" gap
        if (gapType.Contains("method") && !string.IsNullOrWhiteSpace(analysis.Method))
            return $"Method used: {analysis.Method}";

        // Prefer metric for "evaluation" gap
        if (gapType.Contains("evaluation") && !string.IsNullOrWhiteSpace(analysis.Metric))
            return $"Evaluation metric: {analysis.Metric}";

        // Use research problem if present
        if (!string.IsNullOrWhiteSpace(analysis.ResearchProblem))
            return analysis.ResearchProblem;

        // Use first limitation if available
        if (analysis.Limitations.Any())
            return analysis.Limitations.First();

        // Last resort: the gap's own description (truncated)
        if (!string.IsNullOrWhiteSpace(gap.Description))
            return gap.Description.Length > 300 ? gap.Description.Substring(0, 300) + "..." : gap.Description;

        return "Research gap identified";
    }

    private string DetermineEvidenceType(ResearchGapDto gap)
    {
        var gapType = (gap.GapType ?? string.Empty).ToLowerInvariant();
        return gapType switch
        {
            var g when g.Contains("dataset") => EvidenceTypes.Discussion,
            var g when g.Contains("method") => EvidenceTypes.Discussion,
            var g when g.Contains("evaluation") => EvidenceTypes.Conclusion,
            var g when g.Contains("application") => EvidenceTypes.FutureWork,
            var g when g.Contains("geographic") => EvidenceTypes.Discussion,
            var g when g.Contains("temporal") => EvidenceTypes.Limitation,
            var g when g.Contains("contradiction") => EvidenceTypes.Discussion,
            _ => EvidenceTypes.Discussion
        };
    }

    private string DetermineSectionSource(ResearchGapDto gap)
    {
        var gapType = (gap.GapType ?? string.Empty).ToLowerInvariant();
        return gapType switch
        {
            var g when g.Contains("dataset") => "Methods",
            var g when g.Contains("method") => "Methods",
            var g when g.Contains("evaluation") => "Conclusion",
            var g when g.Contains("application") => "Future Work",
            var g when g.Contains("geographic") => "Discussion",
            var g when g.Contains("temporal") => "Discussion",
            var g when g.Contains("contradiction") => "Discussion",
            _ => "Discussion"
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
