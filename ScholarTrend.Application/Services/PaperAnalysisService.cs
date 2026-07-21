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
        analysis.AnalysisSource = ExtractionSource.AbstractOnly;
        analysis.UsedAbstract = true;
        analysis.AbstractConfidence = CalculateConfidence(extraction);
        analysis.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.PaperAnalyses.UpsertAsync(analysis);
        await _unitOfWork.Context.SaveChangesAsync(ct);

        _logger.LogInformation("Analyzed paper {PaperId} with confidence {Confidence}", paperId, analysis.Confidence);
        return analysis;
    }

    public async Task<PaperAnalysis?> AnalyzePaperHybridAsync(int paperId, string? fullText, CancellationToken ct = default)
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

        if (string.IsNullOrWhiteSpace(paper.Abstract))
        {
            _logger.LogWarning("Paper {PaperId} has no abstract, falling back to abstract-only extraction", paperId);
            return await AnalyzePaperAsync(paperId, ct);
        }

        // Extract sections from full text if available
        var sectionExtractor = new SectionExtractor();
        ExtractedSectionsDto? sections = null;

        if (!string.IsNullOrWhiteSpace(fullText))
        {
            sections = sectionExtractor.ExtractRelevantSections(fullText);
            _logger.LogInformation("Extracted sections for paper {PaperId}: Discussion={HasDiscussion}, Conclusion={HasConclusion}",
                paperId, !string.IsNullOrWhiteSpace(sections.Discussion), !string.IsNullOrWhiteSpace(sections.Conclusion));
        }

        // Perform hybrid extraction
        var hybridResult = await _aiExtractionService.ExtractHybridAsync(
            paper.Abstract,
            sections?.Discussion,
            sections?.Conclusion,
            sections?.Introduction,
            sections?.Methodology,
            ct);

        if (hybridResult == null)
        {
            _logger.LogWarning("Hybrid extraction failed for paper {PaperId}, falling back to abstract-only", paperId);
            return await AnalyzePaperAsync(paperId, ct);
        }

        var analysis = await _unitOfWork.PaperAnalyses.GetByPaperIdAsync(paperId)
            ?? new PaperAnalysis { PaperId = paperId };

        var merged = hybridResult.MergedExtraction;

        analysis.ResearchProblem = merged.ResearchProblem;
        analysis.Method = merged.Methods.FirstOrDefault();
        analysis.Dataset = merged.Datasets.FirstOrDefault();
        analysis.Metric = merged.Metric;
        analysis.Contribution = merged.Contribution;
        analysis.MethodsJson = JsonSerializer.Serialize(merged.Methods);
        analysis.DatasetsJson = JsonSerializer.Serialize(merged.Datasets);
        analysis.LimitationsJson = JsonSerializer.Serialize(merged.Limitations);
        analysis.FutureWorkJson = JsonSerializer.Serialize(merged.FutureWork);
        analysis.DiscussionsJson = JsonSerializer.Serialize(merged.Discussions);
        analysis.ConclusionsJson = JsonSerializer.Serialize(merged.Conclusions);
        analysis.Confidence = hybridResult.Metadata.ConfidenceBreakdown.OverallConfidence;
        analysis.AnalysisLevel = quality.HasPdf ? AnalysisLevels.FullText : AnalysisLevels.Abstract;

        // Hybrid extraction metadata
        analysis.UsedAbstract = hybridResult.Metadata.UsedAbstract;
        analysis.UsedDiscussion = hybridResult.Metadata.UsedDiscussion;
        analysis.UsedConclusion = hybridResult.Metadata.UsedConclusion;
        analysis.AbstractConfidence = hybridResult.Metadata.ConfidenceBreakdown.AbstractConfidence;
        analysis.DiscussionConfidence = hybridResult.Metadata.ConfidenceBreakdown.DiscussionConfidence;
        analysis.ConclusionConfidence = hybridResult.Metadata.ConfidenceBreakdown.ConclusionConfidence;

        // Determine analysis source
        if (hybridResult.Metadata.UsedDiscussion || hybridResult.Metadata.UsedConclusion)
            analysis.AnalysisSource = ExtractionSource.Hybrid;
        else
            analysis.AnalysisSource = ExtractionSource.AbstractOnly;

        // Store full metadata as JSON for future reference
        analysis.HybridMetadataJson = JsonSerializer.Serialize(hybridResult.Metadata);

        analysis.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.PaperAnalyses.UpsertAsync(analysis);
        await _unitOfWork.Context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Hybrid analyzed paper {PaperId} with confidence {Confidence} (Abstract: {AbstractConf}, Discussion: {DiscussionConf})",
            paperId, analysis.Confidence, analysis.AbstractConfidence, analysis.DiscussionConfidence);

        return analysis;
    }

    public async Task<HybridExtractionResultDto?> GetHybridAnalysisAsync(int paperId, CancellationToken ct = default)
    {
        var paper = await _unitOfWork.ResearchPapers.GetByIdAsync(paperId);
        if (paper == null || string.IsNullOrWhiteSpace(paper.Abstract))
            return null;

        var sectionExtractor = new SectionExtractor();

        // For now, return the stored metadata if available
        var analysis = await _unitOfWork.PaperAnalyses.GetByPaperIdAsync(paperId);
        if (analysis != null && !string.IsNullOrWhiteSpace(analysis.HybridMetadataJson))
        {
            try
            {
                var metadata = JsonSerializer.Deserialize<ExtractionMetadataDto>(analysis.HybridMetadataJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return new HybridExtractionResultDto
                {
                    MergedExtraction = new AiPaperExtractionDto
                    {
                        Methods = DeserializeList(analysis.MethodsJson),
                        Datasets = DeserializeList(analysis.DatasetsJson),
                        Limitations = DeserializeList(analysis.LimitationsJson),
                        FutureWork = DeserializeList(analysis.FutureWorkJson),
                        Discussions = DeserializeList(analysis.DiscussionsJson),
                        Conclusions = DeserializeList(analysis.ConclusionsJson),
                        ResearchProblem = analysis.ResearchProblem,
                        Metric = analysis.Metric,
                        Contribution = analysis.Contribution
                    },
                    Metadata = metadata ?? new ExtractionMetadataDto()
                };
            }
            catch
            {
                // Fall through to fresh extraction
            }
        }

        // Fresh extraction with available data
        return await _aiExtractionService.ExtractHybridAsync(paper.Abstract, null, null, null, null, ct);
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

    public async Task<int> AnalyzePapersByTopicHybridAsync(int topicId, CancellationToken ct = default)
    {
        var papers = await _unitOfWork.ResearchPapers.GetPapersByTopicAsync(topicId);
        var analyzed = 0;

        foreach (var paper in papers.Take(BatchSize))
        {
            var existing = await _unitOfWork.PaperAnalyses.GetByPaperIdAsync(paper.Id);
            if (existing == null || existing.AnalysisSource != ExtractionSource.Hybrid)
            {
                // Get full text if available (from paper quality)
                string? fullText = null;
                var quality = await _unitOfWork.PaperQualities.GetByPaperIdAsync(paper.Id);
                if (quality?.HasPdf == true)
                {
                    // TODO: Implement PDF text extraction if needed
                    // For now, we rely on the abstract + sections from other sources
                    _logger.LogDebug("Paper {PaperId} has PDF but full text extraction not yet implemented", paper.Id);
                }

                await AnalyzePaperHybridAsync(paper.Id, fullText, ct);
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

        var dtoAnalyses = recentAnalyses.Select(MapToDto).ToList();

        // Calculate hybrid extraction stats
        var allAnalyses = analyses.ToList();
        var hybridStats = new HybridExtractionStatsDto
        {
            HybridAnalyzedPapers = allAnalyses.Count(a => a.AnalysisSource == ExtractionSource.Hybrid),
            AbstractOnlyPapers = allAnalyses.Count(a => a.AnalysisSource == ExtractionSource.AbstractOnly),
            UsedDiscussionPapers = allAnalyses.Count(a => a.UsedDiscussion),
            UsedConclusionPapers = allAnalyses.Count(a => a.UsedConclusion),
            AverageAbstractConfidence = allAnalyses.Any() ? allAnalyses.Average(a => a.AbstractConfidence) : 0,
            AverageDiscussionConfidence = allAnalyses.Any(a => a.DiscussionConfidence > 0)
                ? allAnalyses.Where(a => a.DiscussionConfidence > 0).Average(a => a.DiscussionConfidence)
                : 0,
            AverageOverallConfidence = allAnalyses.Any() ? allAnalyses.Average(a => a.Confidence) : 0
        };

        return new GapAnalysisResultDto
        {
            TopicId = topicId,
            TotalPapers = papers.Count,
            AnalyzedPapers = analyses.Count,
            PendingPapers = papers.Count - analyses.Count,
            FailedPapers = 0,
            AnalysisProgress = papers.Count > 0 ? (analyses.Count * 100.0 / papers.Count) : 0,
            RecentAnalyses = dtoAnalyses,
            HybridStats = hybridStats
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
            AnalyzedAt = a.CreatedAt,

            // Hybrid extraction metadata
            AnalysisSource = a.AnalysisSource,
            UsedDiscussion = a.UsedDiscussion,
            UsedConclusion = a.UsedConclusion,
            AbstractConfidence = a.AbstractConfidence,
            DiscussionConfidence = a.DiscussionConfidence,
            ConclusionConfidence = a.ConclusionConfidence
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
