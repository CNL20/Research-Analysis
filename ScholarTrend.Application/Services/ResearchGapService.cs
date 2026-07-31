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

    public async Task<ResearchGapReportDto> GetGapReportAsync(int topicId, CancellationToken ct = default)
    {
        var topic = await _unitOfWork.Topics.GetByIdAsync(topicId);
        if (topic == null)
            throw new ArgumentException($"Topic {topicId} not found");

        var gaps = await _unitOfWork.ResearchGaps.GetByTopicIdAsync(topicId);
        var patterns = await _patternMiningService.GetStoredPatternsAsync(topicId, ct);
        var timeline = await BuildGapTimelineAsync(topicId, ct);

        // Read path: use last stored coverage — do NOT recompute over all papers (that was slowing topic page).
        var coverage = await _coverageReportService.GetLatestReportAsync(topicId)
            ?? new CoverageReportDto
            {
                TopicId = topicId,
                TopicName = topic.TopicName
            };

        var generatedAt = gaps.Count > 0
            ? gaps.Max(g => g.GeneratedAt)
            : (DateTime?)null;

        // Top sample analyses — used for coverage badge + auto-stale when literature moves on
        var samplePaperIds = await _unitOfWork.ResearchPapers
            .GetTopPaperIdsForTopicSampleAsync(topicId, SampleCoverageLevels.SampleTarget);
        var sampleAnalyses = samplePaperIds.Count > 0
            ? await _unitOfWork.PaperAnalyses.GetByPaperIdsAsync(samplePaperIds)
            : [];

        var sampleSize = samplePaperIds.Count;
        var analyzedInSample = sampleAnalyses.Count;
        var (coverageLevel, coverageLabel, coverageMessage) =
            SampleCoverageLevels.FromCounts(analyzedInSample, Math.Max(sampleSize, 1));

        var (isStale, reason) = EvaluateStaleForSample(gaps, sampleAnalyses);

        return new ResearchGapReportDto
        {
            TopicId = topicId,
            TopicName = topic.TopicName,
            Gaps = gaps.Select(MapToDto).ToList(),
            Patterns = patterns,
            Timeline = timeline,
            Coverage = coverage,
            GeneratedAt = generatedAt,
            Source = "cache",
            NeedsGeneration = gaps.Count == 0,
            IsStale = isStale,
            StaleReason = reason,
            AnalysisCount = analyzedInSample,
            SampleSize = sampleSize,
            AnalyzedInSample = analyzedInSample,
            SampleCoverageLevel = coverageLevel,
            SampleCoverageLabel = coverageLabel,
            SampleCoverageMessage = coverageMessage
        };
    }

    public async Task<ResearchGapReportDto> GenerateGapReportAsync(
        int topicId,
        bool force = false,
        CancellationToken ct = default)
    {
        var topic = await _unitOfWork.Topics.GetByIdAsync(topicId);
        if (topic == null)
            throw new ArgumentException($"Topic {topicId} not found");

        if (!force)
        {
            var cached = await GetGapReportAsync(topicId, ct);
            if (!cached.NeedsGeneration && !cached.IsStale)
            {
                _logger.LogInformation(
                    "Returning cached gap report for topic {TopicId} ({GapCount} gaps)",
                    topicId, cached.Gaps.Count);
                return cached;
            }
        }

        _logger.LogInformation(
            "Generating research gap report for topic {TopicId} ({TopicName}), force={Force}",
            topicId, topic.TopicName, force);

        // Top ≤N by recency + citations; only use papers already extracted for AI.
        var samplePaperIds = await _unitOfWork.ResearchPapers
            .GetTopPaperIdsForTopicSampleAsync(topicId, SampleCoverageLevels.SampleTarget);
        var sampleSize = samplePaperIds.Count;

        var sampleAnalysesEntities = await _unitOfWork.PaperAnalyses.GetByPaperIdsAsync(samplePaperIds);
        var analyses = MapAnalyses(sampleAnalysesEntities);
        var analyzedInSample = analyses.Count;

        // If Top-N is dominated by unextracted / bad-year papers, fall back to papers that
        // already have PaperAnalysis so gap generation is not starved to 0.
        if (analyzedInSample == 0)
        {
            var analyzedIds = await _unitOfWork.ResearchPapers
                .GetTopAnalyzedPaperIdsForTopicSampleAsync(topicId, SampleCoverageLevels.SampleTarget);
            if (analyzedIds.Count > 0)
            {
                _logger.LogWarning(
                    "Topic {TopicId}: Top sample has 0 analyses — falling back to {Count} already-analyzed papers",
                    topicId, analyzedIds.Count);
                samplePaperIds = analyzedIds;
                sampleSize = analyzedIds.Count;
                sampleAnalysesEntities = await _unitOfWork.PaperAnalyses.GetByPaperIdsAsync(samplePaperIds);
                analyses = MapAnalyses(sampleAnalysesEntities);
                analyzedInSample = analyses.Count;
            }
        }

        var (coverageLevel, coverageLabel, coverageMessage) =
            SampleCoverageLevels.FromCounts(analyzedInSample, Math.Max(sampleSize, 1));

        _logger.LogInformation(
            "Gap sample for topic {TopicId}: {Analyzed}/{SampleSize} analyzed ({Level})",
            topicId, analyzedInSample, sampleSize, coverageLevel);

        PatternMiningResultDto patterns;
        if (samplePaperIds.Count > 0)
        {
            patterns = await _patternMiningService.MinePatternsForPaperIdsAsync(
                topicId, samplePaperIds, ct);
        }
        else
        {
            patterns = new PatternMiningResultDto
            {
                TopicId = topicId,
                TopicName = topic.TopicName,
                MinedAt = DateTime.UtcNow
            };
        }

        var timeline = await BuildGapTimelineAsync(topicId, ct);

        List<ResearchGapDto> generatedGaps = [];
        if (analyses.Count > 0)
        {
            var trimmedPatterns = TrimPatternsForAi(patterns);
            var trimmedTimeline = TrimTimelineForAi(timeline);

            if (analyses.Count >= ChunkAnalysisThreshold)
            {
                _logger.LogInformation(
                    "Topic {TopicId} sample has {Count} analyses — using year-chunked gap generation",
                    topicId, analyses.Count);
                generatedGaps = await GenerateGapsChunkedAsync(
                    topic.TopicName, trimmedPatterns, trimmedTimeline, analyses, ct);
            }
            else
            {
                var sampled = SampleAnalysesForAi(analyses, MaxPapersPerPrompt);
                generatedGaps = await _aiExtractionService.GenerateResearchGapsAsync(
                    topic.TopicName,
                    trimmedPatterns,
                    trimmedTimeline,
                    sampled,
                    ct);
            }

            if (generatedGaps.Count == 0)
            {
                _logger.LogWarning(
                    "AI returned 0 gaps for topic {TopicId} with {AnalysisCount} analyses — using pattern-based fallback gaps",
                    topicId, analyses.Count);
                generatedGaps = BuildHeuristicGapsFromPatterns(topic.TopicName, patterns, analyses);
            }
        }
        else
        {
            _logger.LogWarning(
                "No PaperAnalysis in Top-{Sample} for topic {TopicId}; skipping AI gap generation",
                SampleCoverageLevels.SampleTarget, topicId);
        }

        // Never wipe existing gaps when AI produced nothing (parse fail / empty / API error).
        if (generatedGaps.Count == 0)
        {
            var existing = await _unitOfWork.ResearchGaps.GetByTopicIdAsync(topicId);
            if (existing.Count > 0)
            {
                _logger.LogWarning(
                    "Gap AI returned 0 gaps for topic {TopicId}; keeping {Existing} existing gaps",
                    topicId, existing.Count);
                var cached = await GetGapReportAsync(topicId, ct);
                cached.Source = "cache";
                cached.NeedsGeneration = false;
                return cached;
            }

            _logger.LogWarning(
                "Gap AI returned 0 gaps for topic {TopicId} and no existing gaps to keep (analyses={Analyzed})",
                topicId, analyses.Count);
        }

        await _unitOfWork.ResearchGaps.DeleteByTopicAsync(topicId);
        await _unitOfWork.Context.SaveChangesAsync(ct);

        var savedGaps = generatedGaps.Count > 0
            ? await SaveGapsWithEvidenceAsync(topicId, generatedGaps, analyses, ct)
            : [];

        var coverage = await _coverageReportService.GetLatestReportAsync(topicId)
            ?? new CoverageReportDto
            {
                TopicId = topicId,
                TopicName = topic.TopicName
            };
        // Do NOT call GenerateReportAsync here — it loads every paper in the topic
        // (thousands for AI) and made pipeline re-runs take ~100s even with cache intent.

        return new ResearchGapReportDto
        {
            TopicId = topicId,
            TopicName = topic.TopicName,
            Gaps = savedGaps,
            Patterns = patterns,
            Timeline = timeline,
            Coverage = coverage,
            GeneratedAt = DateTime.UtcNow,
            Source = "generated",
            NeedsGeneration = savedGaps.Count == 0,
            IsStale = false,
            AnalysisCount = analyzedInSample,
            SampleSize = sampleSize,
            AnalyzedInSample = analyzedInSample,
            SampleCoverageLevel = coverageLevel,
            SampleCoverageLabel = coverageLabel,
            SampleCoverageMessage = coverageMessage
        };
    }

    private async Task<(int SampleSize, int AnalyzedInSample, string Level, string Label, string? Message)>
        BuildSampleCoverageAsync(int topicId, CancellationToken ct)
    {
        var samplePaperIds = await _unitOfWork.ResearchPapers
            .GetTopPaperIdsForTopicSampleAsync(topicId, SampleCoverageLevels.SampleTarget);
        var sampleSize = samplePaperIds.Count;
        if (sampleSize == 0)
        {
            var empty = SampleCoverageLevels.FromCounts(0, SampleCoverageLevels.SampleTarget);
            return (0, 0, empty.Level, empty.Label, empty.Message);
        }

        var analyses = await _unitOfWork.PaperAnalyses.GetByPaperIdsAsync(samplePaperIds);
        var analyzed = analyses.Count;
        var (level, label, message) = SampleCoverageLevels.FromCounts(analyzed, sampleSize);
        return (sampleSize, analyzed, level, label, message);
    }

    private static string Truncate(string? value, int maxLen)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLen ? value : value[..maxLen];
    }

    /// <summary>
    /// When Groq returns empty/unparsable gaps, still produce usable gaps from mined patterns
    /// so the pipeline does not complete with gaps=0 while analyses exist.
    /// </summary>
    private static List<ResearchGapDto> BuildHeuristicGapsFromPatterns(
        string topicName,
        PatternMiningResultDto patterns,
        List<PaperAnalysisDto> analyses)
    {
        var paperIds = analyses.Select(a => a.PaperId).Take(5).ToList();
        var gaps = new List<ResearchGapDto>();

        foreach (var lim in patterns.Limitations.OrderByDescending(l => l.PaperCount).Take(3))
        {
            gaps.Add(new ResearchGapDto
            {
                Title = Truncate($"Address limitation: {lim.LimitationText}", 500),
                Description =
                    $"Across analyzed papers in {topicName}, the limitation pattern \"{lim.LimitationText}\" " +
                    $"appears in approximately {lim.PaperCount} papers. This suggests an open research opportunity " +
                    "to design methods or evaluations that directly mitigate this weakness.",
                GapType = GapTypes.Method,
                SuggestedDirection =
                    $"Propose and evaluate approaches that specifically overcome \"{lim.LimitationText}\" " +
                    $"in the context of {topicName}, with comparative benchmarks against current methods.",
                Confidence = Math.Clamp(40 + lim.PaperCount * 5, 45, 85),
                EvidenceCount = Math.Min(paperIds.Count, 3),
                SupportingPaperIds = paperIds.Take(3).ToList()
            });
        }

        foreach (var method in patterns.Methods.OrderByDescending(m => m.PaperCount).Take(2))
        {
            gaps.Add(new ResearchGapDto
            {
                Title = Truncate($"Evaluation gap for {method.MethodName}", 500),
                Description =
                    $"The method \"{method.MethodName}\" is frequent in the {topicName} sample ({method.PaperCount} papers), " +
                    "but systematic cross-dataset or real-world evaluation remains under-explored relative to its adoption.",
                GapType = GapTypes.Evaluation,
                SuggestedDirection =
                    $"Design a multi-dataset / multi-scenario benchmark study for \"{method.MethodName}\" " +
                    $"within {topicName} and report failure modes and fairness metrics.",
                Confidence = Math.Clamp(40 + method.PaperCount * 3, 45, 80),
                EvidenceCount = Math.Min(paperIds.Count, 3),
                SupportingPaperIds = paperIds.Take(3).ToList()
            });
        }

        foreach (var ds in patterns.Datasets.OrderByDescending(d => d.PaperCount).Take(1))
        {
            gaps.Add(new ResearchGapDto
            {
                Title = Truncate($"Dataset diversity beyond {ds.DatasetName}", 500),
                Description =
                    $"Dataset \"{ds.DatasetName}\" dominates the sample ({ds.PaperCount} papers). " +
                    "Over-reliance on a small set of benchmarks can hide generalization gaps.",
                GapType = GapTypes.Dataset,
                SuggestedDirection =
                    $"Curate or adopt complementary datasets beyond \"{ds.DatasetName}\" and re-evaluate " +
                    $"top methods in {topicName} under distribution shift.",
                Confidence = Math.Clamp(40 + ds.PaperCount * 3, 45, 80),
                EvidenceCount = Math.Min(paperIds.Count, 3),
                SupportingPaperIds = paperIds.Take(3).ToList()
            });
        }

        if (gaps.Count == 0 && analyses.Count > 0)
        {
            gaps.Add(new ResearchGapDto
            {
                Title = $"Open challenges in {topicName}",
                Description =
                    $"Based on {analyses.Count} analyzed papers in the current sample for {topicName}, " +
                    "there is limited structured coverage of limitations and future work. " +
                    "Further synthesis is needed to surface method, dataset, and evaluation gaps.",
                GapType = GapTypes.Application,
                SuggestedDirection =
                    $"Expand paper analysis coverage for {topicName} and regenerate gap reports " +
                    "after more limitations/future-work fields are extracted.",
                Confidence = 50,
                EvidenceCount = Math.Min(paperIds.Count, 3),
                SupportingPaperIds = paperIds.Take(3).ToList()
            });
        }

        return gaps.Take(MaxFinalGaps).ToList();
    }

    private static List<PaperAnalysisDto> MapAnalyses(List<PaperAnalysis> analyses) =>
        analyses.Select(a => new PaperAnalysisDto
        {
            PaperId = a.PaperId,
            Title = a.Paper?.Title ?? "",
            Year = a.Paper?.PublicationYear ?? 0,
            ResearchProblem = a.ResearchProblem,
            Method = a.Method,
            Dataset = a.Dataset,
            Limitations = DeserializeListStatic(a.LimitationsJson),
            FutureWork = DeserializeListStatic(a.FutureWorkJson),
            Confidence = a.Confidence
        }).ToList();

    private static List<string> DeserializeListStatic(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private const int ChunkAnalysisThreshold = 60;
    private const int MaxPapersPerPrompt = 40;
    private const int TopMethods = 20;
    private const int TopDatasets = 20;
    private const int TopLimitations = 30;
    private const int MaxYearsPerChunk = 3;
    private const int MaxFinalGaps = 7;
    private const double StaleGrowthRatio = 0.2;

    /// <summary>
    /// Gaps are stale when the Top sample has moved on: any analysis created/updated
    /// after the last gap generation, or gaps older than 30 days.
    /// </summary>
    private static (bool IsStale, string? Reason) EvaluateStaleForSample(
        List<ResearchGap> gaps,
        List<PaperAnalysis> sampleAnalyses)
    {
        if (gaps.Count == 0)
            return (false, null);

        var generatedAt = gaps.Max(g => g.GeneratedAt);

        if (generatedAt < DateTime.UtcNow.AddDays(-30))
        {
            return (true, "Stored gaps are older than 30 days. Consider regenerating for current trends.");
        }

        // Only brand-new extracts (CreatedAt after gap gen) make gaps stale.
        // Do NOT use UpdatedAt — minor re-saves would force AI gap every pipeline run.
        var newerInSample = sampleAnalyses.Count(a =>
            a.CreatedAt > generatedAt.AddSeconds(2));

        if (newerInSample > 0)
        {
            return (true,
                $"{newerInSample} new paper analysis(es) in the Top sample since gaps were generated — regenerate for current trends.");
        }

        return (false, null);
    }

    private static (bool IsStale, string? Reason) EvaluateStaleByAge(List<ResearchGap> gaps)
    {
        if (gaps.Count == 0)
            return (false, null);

        var generatedAt = gaps.Max(g => g.GeneratedAt);
        if (generatedAt < DateTime.UtcNow.AddDays(-30))
        {
            return (true, "Stored gaps are older than 30 days. Consider regenerating.");
        }

        return (false, null);
    }

    private static (bool IsStale, string? Reason) EvaluateStale(
        List<ResearchGap> gaps,
        List<PaperAnalysis> analyses)
    {
        if (gaps.Count == 0)
            return (false, null);

        var generatedAt = gaps.Max(g => g.GeneratedAt);
        var newerAnalyses = analyses.Count(a =>
            (a.UpdatedAt ?? a.CreatedAt) > generatedAt);

        if (analyses.Count == 0)
            return (false, null);

        var ratio = newerAnalyses / (double)Math.Max(analyses.Count, 1);
        if (newerAnalyses >= 5 && ratio >= StaleGrowthRatio)
        {
            return (true,
                $"{newerAnalyses} paper analyses were updated after gaps were generated ({ratio:P0} of topic analyses).");
        }

        // Also stale if gaps are older than 30 days and there is any newer analysis
        if (newerAnalyses > 0 && generatedAt < DateTime.UtcNow.AddDays(-30))
        {
            return (true, "Stored gaps are older than 30 days and newer paper analyses exist.");
        }

        return (false, null);
    }

    private static PatternMiningResultDto TrimPatternsForAi(PatternMiningResultDto patterns) => new()
    {
        TopicId = patterns.TopicId,
        TopicName = patterns.TopicName,
        MinedAt = patterns.MinedAt,
        Methods = patterns.Methods.OrderByDescending(m => m.PaperCount).Take(TopMethods).ToList(),
        Datasets = patterns.Datasets.OrderByDescending(d => d.PaperCount).Take(TopDatasets).ToList(),
        Limitations = patterns.Limitations.OrderByDescending(l => l.PaperCount).Take(TopLimitations).ToList()
    };

    private static GapTimelineDto TrimTimelineForAi(GapTimelineDto timeline) => new()
    {
        TopicId = timeline.TopicId,
        TopicName = timeline.TopicName,
        Timeline = timeline.Timeline
            .OrderByDescending(t => t.Year)
            .ThenByDescending(t => t.PaperCount)
            .Take(40)
            .ToList()
    };

    private static List<PaperAnalysisDto> SampleAnalysesForAi(
        List<PaperAnalysisDto> analyses,
        int maxCount)
    {
        if (analyses.Count <= maxCount)
            return analyses;

        // Prefer papers with limitations / future work, then confidence, then recent year
        return analyses
            .OrderByDescending(a => a.Limitations.Count > 0 ? 1 : 0)
            .ThenByDescending(a => a.FutureWork.Count > 0 ? 1 : 0)
            .ThenByDescending(a => a.Confidence)
            .ThenByDescending(a => a.Year)
            .Take(maxCount)
            .ToList();
    }

    private async Task<List<ResearchGapDto>> GenerateGapsChunkedAsync(
        string topicName,
        PatternMiningResultDto patterns,
        GapTimelineDto timeline,
        List<PaperAnalysisDto> analyses,
        CancellationToken ct)
    {
        var chunks = BuildYearChunks(analyses);
        var rawGaps = new List<ResearchGapDto>();

        foreach (var chunk in chunks)
        {
            ct.ThrowIfCancellationRequested();

            var yearFrom = chunk.Min(a => a.Year);
            var yearTo = chunk.Max(a => a.Year);
            var sampled = SampleAnalysesForAi(chunk, MaxPapersPerPrompt);

            var scopedTimeline = new GapTimelineDto
            {
                TopicId = timeline.TopicId,
                TopicName = timeline.TopicName,
                Timeline = timeline.Timeline
                    .Where(t => t.Year >= yearFrom && t.Year <= yearTo)
                    .ToList()
            };

            _logger.LogInformation(
                "Gap chunk {YearFrom}-{YearTo}: {PaperCount} papers (sampled {Sampled})",
                yearFrom, yearTo, chunk.Count, sampled.Count);

            var chunkGaps = await _aiExtractionService.GenerateResearchGapsAsync(
                $"{topicName} (period {yearFrom}-{yearTo})",
                patterns,
                scopedTimeline.Timeline.Count > 0 ? scopedTimeline : timeline,
                sampled,
                ct);

            rawGaps.AddRange(chunkGaps);
        }

        var merged = MergeAndDedupeGaps(rawGaps);
        _logger.LogInformation(
            "Chunked generation produced {Raw} raw gaps → {Merged} after merge",
            rawGaps.Count, merged.Count);
        return merged;
    }

    private static List<List<PaperAnalysisDto>> BuildYearChunks(List<PaperAnalysisDto> analyses)
    {
        var byYear = analyses
            .Where(a => a.Year > 0)
            .GroupBy(a => a.Year)
            .OrderBy(g => g.Key)
            .ToList();

        var unknownYear = analyses.Where(a => a.Year <= 0).ToList();
        var chunks = new List<List<PaperAnalysisDto>>();
        var current = new List<PaperAnalysisDto>();
        var yearsInChunk = 0;

        foreach (var yearGroup in byYear)
        {
            if (yearsInChunk >= MaxYearsPerChunk && current.Count > 0)
            {
                chunks.Add(current);
                current = [];
                yearsInChunk = 0;
            }

            current.AddRange(yearGroup);
            yearsInChunk++;

            // Also split if chunk already huge
            if (current.Count >= ChunkAnalysisThreshold)
            {
                chunks.Add(current);
                current = [];
                yearsInChunk = 0;
            }
        }

        if (current.Count > 0)
            chunks.Add(current);

        if (unknownYear.Count > 0)
        {
            if (chunks.Count == 0)
                chunks.Add(unknownYear);
            else
                chunks[^1].AddRange(unknownYear);
        }

        if (chunks.Count == 0)
            chunks.Add(analyses);

        return chunks;
    }

    private static List<ResearchGapDto> MergeAndDedupeGaps(List<ResearchGapDto> gaps)
    {
        if (gaps.Count == 0) return gaps;

        static string Norm(string? s) =>
            string.Join(' ', (s ?? string.Empty).ToLowerInvariant()
                .Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
                .ToArray())
            .Trim();

        var groups = new Dictionary<string, ResearchGapDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var gap in gaps)
        {
            var titleKey = Norm(gap.Title);
            if (titleKey.Length > 48) titleKey = titleKey[..48];
            var key = $"{Norm(gap.GapType)}|{titleKey}";

            if (!groups.TryGetValue(key, out var existing))
            {
                groups[key] = gap;
                continue;
            }

            // Keep higher confidence; merge supporting papers
            if (gap.Confidence > existing.Confidence)
            {
                var papers = existing.SupportingPaperIds
                    .Concat(gap.SupportingPaperIds)
                    .Distinct()
                    .ToList();
                gap.SupportingPaperIds = papers;
                gap.EvidenceCount = Math.Max(gap.EvidenceCount, papers.Count);
                if (string.IsNullOrWhiteSpace(gap.SuggestedDirection))
                    gap.SuggestedDirection = existing.SuggestedDirection;
                groups[key] = gap;
            }
            else
            {
                existing.SupportingPaperIds = existing.SupportingPaperIds
                    .Concat(gap.SupportingPaperIds)
                    .Distinct()
                    .ToList();
                existing.EvidenceCount = Math.Max(existing.EvidenceCount, existing.SupportingPaperIds.Count);
            }
        }

        // Prefer diversity: at most 2 per gap type, then top by confidence
        return groups.Values
            .GroupBy(g => Norm(g.GapType))
            .SelectMany(g => g.OrderByDescending(x => x.Confidence).Take(2))
            .OrderByDescending(g => g.Confidence)
            .Take(MaxFinalGaps)
            .ToList();
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
                Title = Truncate(gap.Title, 500),
                Description = gap.Description ?? "",
                GapType = Truncate(string.IsNullOrWhiteSpace(gap.GapType) ? GapTypes.Dataset : gap.GapType, 50),
                SuggestedDirection = gap.SuggestedDirection ?? "",
                EvidenceCount = gap.EvidenceCount,
                Confidence = gap.Confidence,
                ConfidenceLevel = Truncate(ConfidenceLevels.GetLevel(gap.Confidence), 20),
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
