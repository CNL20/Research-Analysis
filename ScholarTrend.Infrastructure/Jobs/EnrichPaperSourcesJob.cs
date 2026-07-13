using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.Aggregation;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Services.Aggregation;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Jobs;

/// <summary>
/// Background job that enriches an existing <see cref="ResearchPaper"/>
/// with metadata from the bibliographic sources that were not captured
/// during the initial import. Triggered by <see cref="IEnrichPaperSourcesEnqueuer"/>.
/// </summary>
public class EnrichPaperSourcesJob
{
    private readonly ScholarTrendDbContext _context;
    private readonly IEnrichmentFetcher _fetcher;
    private readonly IArxivDoiResolver _arxivDoi;
    private readonly IPaperKeywordLinkerService _keywordLinker;
    private readonly IPaperAuthorLinkerService _authorLinker;
    private readonly IJournalResolver _journalResolver;
    private readonly INotificationService _notificationService;
    private readonly ILogger<EnrichPaperSourcesJob> _logger;

    public EnrichPaperSourcesJob(
        ScholarTrendDbContext context,
        IEnrichmentFetcher fetcher,
        IArxivDoiResolver arxivDoi,
        IPaperKeywordLinkerService keywordLinker,
        IPaperAuthorLinkerService authorLinker,
        IJournalResolver journalResolver,
        INotificationService notificationService,
        ILogger<EnrichPaperSourcesJob> logger)
    {
        _context = context;
        _fetcher = fetcher;
        _arxivDoi = arxivDoi;
        _keywordLinker = keywordLinker;
        _authorLinker = authorLinker;
        _journalResolver = journalResolver;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task EnrichAsync(int paperId)
    {
        var fetchErrors = new List<string>();

        try
        {
            var paper = await _context.ResearchPapers
                .Include(p => p.PaperSources)
                .FirstOrDefaultAsync(p => p.Id == paperId);

            if (paper == null)
            {
                _logger.LogWarning("Enrich job: paper {PaperId} not found", paperId);
                await _notificationService.NotifyAdminsPaperEnrichmentIssueAsync(
                    paperId,
                    $"Paper #{paperId}",
                    ["paper not found"],
                    fetchErrors);
                return;
            }

            var paperTitle = paper.Title;
            var sources = new Dictionary<string, PaperSourceMetadataDto>(StringComparer.OrdinalIgnoreCase);

            foreach (var ps in paper.PaperSources)
            {
                if (string.IsNullOrEmpty(ps.RawMetadataJson))
                {
                    continue;
                }

                try
                {
                    var dto = JsonSerializer.Deserialize<PaperSourceMetadataDto>(ps.RawMetadataJson);
                    if (dto != null && dto.Found)
                    {
                        sources[SourceNameNormalizer.ToMergeKey(ps.SourceName)] = dto;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to deserialize cached metadata for paper {PaperId} source {Source}",
                        paperId, ps.SourceName);
                    fetchErrors.Add($"{ps.SourceName}: invalid cached metadata");
                }
            }

            if (string.IsNullOrEmpty(paper.Doi))
            {
                var arxivPs = paper.PaperSources.FirstOrDefault(p => p.SourceName == "ArXiv");
                if (arxivPs != null)
                {
                    var doi = await _arxivDoi.ResolveDoiAsync(arxivPs.ExternalId);
                    if (!string.IsNullOrEmpty(doi))
                    {
                        paper.Doi = doi;
                        _logger.LogInformation(
                            "Resolved ArXiv {ArxivId} -> DOI {Doi} during enrichment",
                            arxivPs.ExternalId, doi);
                    }
                }
            }

            if (string.IsNullOrEmpty(paper.Doi))
            {
                fetchErrors.Add("enrich: paper has no DOI — skipped OpenAlex/Crossref/Semantic Scholar fetch");
            }
            else
            {
                var fetchTasks = new List<Task>();
                if (ShouldFetchSource(sources, "openalex"))
                {
                    fetchTasks.Add(SafeFetchAsync("openalex",
                        () => _fetcher.FetchOpenAlexAsync(paper.Doi!), sources, fetchErrors));
                }

                if (ShouldFetchSource(sources, "semantic_scholar"))
                {
                    fetchTasks.Add(SafeFetchAsync("semantic_scholar",
                        () => _fetcher.FetchSemanticScholarAsync(paper.Doi!), sources, fetchErrors));
                }

                if (ShouldFetchSource(sources, "crossref"))
                {
                    fetchTasks.Add(SafeFetchAsync("crossref",
                        () => _fetcher.FetchCrossrefAsync(paper.Doi!), sources, fetchErrors));
                }

                if (fetchTasks.Count > 0)
                {
                    try
                    {
                        await Task.WhenAll(fetchTasks);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Some enrichment fetches failed for paper {PaperId}", paperId);
                        fetchErrors.Add($"enrich: {ex.Message}");
                    }
                }
            }

            var merged = MergedPaperBuilder.MergeFromSources(sources, paper.Doi ?? string.Empty);
            if (merged == null)
            {
                fetchErrors.Add("merge: no usable metadata from any source");
            }
            else
            {
                if (ShouldReplaceAbstract(paper.Abstract, merged.Abstract))
                {
                    paper.Abstract = merged.Abstract;
                }

                if (string.IsNullOrWhiteSpace(paper.PdfUrl) && !string.IsNullOrWhiteSpace(merged.PdfUrl))
                {
                    paper.PdfUrl = merged.PdfUrl;
                }

                if (string.IsNullOrWhiteSpace(paper.Url) && !string.IsNullOrWhiteSpace(merged.Url))
                {
                    paper.Url = merged.Url;
                }

                if (paper.PublicationYear == null && merged.Year.HasValue)
                {
                    paper.PublicationYear = merged.Year;
                }

                if (paper.CitationCount == 0 && merged.CitationCount.HasValue)
                {
                    paper.CitationCount = merged.CitationCount.Value;
                }

                if (!string.IsNullOrWhiteSpace(merged.Journal))
                {
                    var journalId = await _journalResolver.ResolveAsync(merged.Journal);
                    if (journalId.HasValue)
                    {
                        paper.JournalId = journalId.Value;
                    }
                }

                if (merged.AuthorNames is { Count: > 0 })
                {
                    await _authorLinker.LinkAuthorsAsync(paperId, merged.AuthorNames);
                }

                if (merged.Keywords is { Count: > 0 })
                {
                    await _keywordLinker.LinkKeywordsAsync(paperId, merged.Keywords);
                }

                await _keywordLinker.LinkFromContextAsync(
                    paperId, paper.Title, paper.Abstract, null, null);

                paper.UpdatedAt = DateTime.UtcNow;
            }

            foreach (var (name, src) in sources)
            {
                if (!src.Found)
                {
                    continue;
                }

                var storageName = SourceNameNormalizer.ToStorageName(name);
                var existing = paper.PaperSources.FirstOrDefault(ps =>
                    SourceNameNormalizer.ToMergeKey(ps.SourceName) == name);

                if (existing != null)
                {
                    existing.LastSeenAt = DateTime.UtcNow;
                    existing.SourceCitationCount = src.CitationCount;
                    existing.SourceYear = src.Year;
                    existing.RawMetadataJson = JsonSerializer.Serialize(src);
                }
                else
                {
                    paper.PaperSources.Add(new PaperSource
                    {
                        SourceName = storageName,
                        ExternalId = src.ExternalId ?? string.Empty,
                        SourceDoi = src.Doi,
                        SourceUrl = src.Url,
                        SourceCitationCount = src.CitationCount,
                        SourceYear = src.Year,
                        FetchedAt = DateTime.UtcNow,
                        LastSeenAt = DateTime.UtcNow,
                        RawMetadataJson = JsonSerializer.Serialize(src)
                    });
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Enriched paper {PaperId} with {Count} sources", paperId, sources.Count);

            await NotifyAdminEnrichmentResultAsync(paperId, paperTitle, sources.Count, fetchErrors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Enrich job failed for paper {PaperId}", paperId);
            fetchErrors.Add($"enrich crashed: {ex.Message}");

            var title = await _context.ResearchPapers
                .Where(p => p.Id == paperId)
                .Select(p => p.Title)
                .FirstOrDefaultAsync() ?? $"Paper #{paperId}";

            await NotifyAdminEnrichmentResultAsync(paperId, title, 0, fetchErrors);
        }
    }

    private async Task NotifyAdminEnrichmentResultAsync(
        int paperId,
        string paperTitle,
        int sourceCount,
        List<string> fetchErrors)
    {
        var paper = await _context.ResearchPapers
            .Include(p => p.Journal)
            .Include(p => p.PaperAuthors)
            .Include(p => p.PaperKeywords)
            .FirstOrDefaultAsync(p => p.Id == paperId);

        if (paper == null)
        {
            return;
        }

        var missingFields = PaperMetadataCompleteness.GetMissingFields(paper);
        if (missingFields.Count == 0)
        {
            await _notificationService.NotifyAdminsPaperEnrichmentCompleteAsync(
                paperId, paperTitle, sourceCount);
            return;
        }

        await _notificationService.NotifyAdminsPaperEnrichmentIssueAsync(
            paperId, paperTitle, missingFields, fetchErrors);
    }

    private async Task SafeFetchAsync(
        string key,
        Func<Task<PaperSourceMetadataDto>> fetch,
        Dictionary<string, PaperSourceMetadataDto> target,
        List<string> fetchErrors)
    {
        try
        {
            var dto = await fetch();
            if (dto != null && dto.Found)
            {
                target[key] = dto;
                return;
            }

            var reason = dto?.ErrorMessage ?? "not found";
            fetchErrors.Add($"{key}: {reason}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fetch failed for source {Source}", key);
            fetchErrors.Add($"{key}: {ex.Message}");
        }
    }

    private static bool ShouldFetchSource(
        Dictionary<string, PaperSourceMetadataDto> sources,
        string key)
    {
        sources.TryGetValue(key, out var cached);
        return MetadataMapper.NeedsRefresh(cached);
    }

    private static bool ShouldReplaceAbstract(string? current, string? merged)
    {
        if (string.IsNullOrWhiteSpace(merged))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(current))
        {
            return true;
        }

        return current.Contains("<jats:", StringComparison.OrdinalIgnoreCase)
            && !merged.Contains("<jats:", StringComparison.OrdinalIgnoreCase);
    }
}
