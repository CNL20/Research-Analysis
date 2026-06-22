using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.Aggregation;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.Services.Aggregation;

namespace ScholarTrend.Application.Services;

public class PaperAggregationService : IPaperAggregationService
{
    private readonly IOpenAlexClient _openAlexClient;
    private readonly ISemanticScholarClient _semanticScholarClient;
    private readonly ICrossrefClient _crossrefClient;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PaperAggregationService> _logger;

    public PaperAggregationService(
        IOpenAlexClient openAlexClient,
        ISemanticScholarClient semanticScholarClient,
        ICrossrefClient crossrefClient,
        IUnitOfWork unitOfWork,
        ILogger<PaperAggregationService> logger)
    {
        _openAlexClient = openAlexClient;
        _semanticScholarClient = semanticScholarClient;
        _crossrefClient = crossrefClient;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PaperAggregateResultDto> AggregateByDoiAsync(string doi)
    {
        var normalizedDoi = MetadataQualityAnalyzer.NormalizeDoi(doi);
        if (string.IsNullOrWhiteSpace(normalizedDoi))
        {
            throw new InvalidOperationException("DOI is required.");
        }

        var internalPaper = await _unitOfWork.ResearchPapers.GetByDoiAsync(normalizedDoi);
        var sources = await FetchAllSourcesAsync(normalizedDoi, internalPaper);
        return MetadataQualityAnalyzer.Analyze(normalizedDoi, sources, internalPaper?.Id);
    }

    public async Task<PaperAggregateResultDto> AggregateByPaperIdAsync(int paperId)
    {
        var paper = await _unitOfWork.ResearchPapers.GetPaperWithDetailsAsync(paperId);
        if (paper == null)
        {
            throw new InvalidOperationException("Paper not found.");
        }

        if (string.IsNullOrWhiteSpace(paper.Doi))
        {
            throw new InvalidOperationException("Paper does not have a DOI for cross-source aggregation.");
        }

        return await AggregateByDoiAsync(paper.Doi);
    }

    private async Task<Dictionary<string, PaperSourceMetadataDto>> FetchAllSourcesAsync(
        string normalizedDoi,
        Domain.Entities.ResearchPaper? internalPaper)
    {
        Task<PaperSourceMetadataDto> internalTask = Task.FromResult(
            internalPaper == null
                ? MetadataMapper.NotFound("internal", "Paper is not stored in ScholarTrend yet.")
                : MetadataMapper.FromInternalPaper(internalPaper));

        var openAlexTask = SafeFetchAsync("openalex", () => _openAlexClient.GetByDoiAsync(normalizedDoi));
        var semanticTask = SafeFetchAsync("semantic_scholar", () => _semanticScholarClient.GetByDoiAsync(normalizedDoi));
        var crossrefTask = SafeFetchAsync("crossref", () => _crossrefClient.GetByDoiAsync(normalizedDoi));

        await Task.WhenAll(internalTask, openAlexTask, semanticTask, crossrefTask);

        return new Dictionary<string, PaperSourceMetadataDto>
        {
            ["internal"] = await internalTask,
            ["openalex"] = await openAlexTask,
            ["semantic_scholar"] = await semanticTask,
            ["crossref"] = await crossrefTask,
        };
    }

    private async Task<PaperSourceMetadataDto> SafeFetchAsync(
        string source,
        Func<Task<PaperSourceMetadataDto>> fetch)
    {
        try
        {
            return await fetch();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch metadata from {Source}", source);
            return MetadataMapper.NotFound(source, $"Failed to fetch metadata from {source}.");
        }
    }
}
