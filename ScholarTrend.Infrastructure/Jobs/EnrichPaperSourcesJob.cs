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
///
/// Behavior:
/// - Reads already-captured metadata from PaperSources.RawMetadataJson (no re-fetch).
/// - Fetches missing sources in parallel via IEnrichmentFetcher (rate-limited + retried).
/// - Updates the paper with non-empty fields only (does NOT overwrite existing values).
/// - Upserts PaperSources rows for each newly-fetched source.
/// </summary>
public class EnrichPaperSourcesJob
{
    private readonly ScholarTrendDbContext _context;
    private readonly IEnrichmentFetcher _fetcher;
    private readonly IArxivDoiResolver _arxivDoi;
    private readonly ILogger<EnrichPaperSourcesJob> _logger;

    public EnrichPaperSourcesJob(
        ScholarTrendDbContext context,
        IEnrichmentFetcher fetcher,
        IArxivDoiResolver arxivDoi,
        ILogger<EnrichPaperSourcesJob> logger)
    {
        _context = context;
        _fetcher = fetcher;
        _arxivDoi = arxivDoi;
        _logger = logger;
    }

    public async Task EnrichAsync(int paperId)
    {
        var paper = await _context.ResearchPapers
            .Include(p => p.PaperSources)
            .FirstOrDefaultAsync(p => p.Id == paperId);

        if (paper == null)
        {
            _logger.LogWarning("Enrich job: paper {PaperId} not found", paperId);
            return;
        }

        var sources = new Dictionary<string, PaperSourceMetadataDto>(StringComparer.OrdinalIgnoreCase);

        // Reuse cached raw metadata first
        foreach (var ps in paper.PaperSources)
        {
            if (string.IsNullOrEmpty(ps.RawMetadataJson)) continue;
            try
            {
                var dto = JsonSerializer.Deserialize<PaperSourceMetadataDto>(ps.RawMetadataJson);
                if (dto != null && dto.Found)
                {
                    sources[ps.SourceName] = dto;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize cached metadata for paper {PaperId} source {Source}",
                    paperId, ps.SourceName);
            }
        }

        // Try to resolve a DOI if missing
        if (string.IsNullOrEmpty(paper.Doi))
        {
            var arxivPs = paper.PaperSources.FirstOrDefault(p => p.SourceName == "ArXiv");
            if (arxivPs != null)
            {
                var doi = await _arxivDoi.ResolveDoiAsync(arxivPs.ExternalId);
                if (!string.IsNullOrEmpty(doi))
                {
                    paper.Doi = doi;
                    _logger.LogInformation("Resolved ArXiv {ArxivId} -> DOI {Doi} during enrichment",
                        arxivPs.ExternalId, doi);
                }
            }
        }

        // Fetch sources that we don't have yet, in parallel
        var fetchTasks = new List<Task>();
        if (!sources.ContainsKey("openalex") && !string.IsNullOrEmpty(paper.Doi))
        {
            fetchTasks.Add(SafeFetchAsync("openalex",
                () => _fetcher.FetchOpenAlexAsync(paper.Doi!), sources));
        }
        if (!sources.ContainsKey("semantic_scholar") && !string.IsNullOrEmpty(paper.Doi))
        {
            fetchTasks.Add(SafeFetchAsync("semantic_scholar",
                () => _fetcher.FetchSemanticScholarAsync(paper.Doi!), sources));
        }
        if (!sources.ContainsKey("crossref") && !string.IsNullOrEmpty(paper.Doi))
        {
            fetchTasks.Add(SafeFetchAsync("crossref",
                () => _fetcher.FetchCrossrefAsync(paper.Doi!), sources));
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
            }
        }

        // Merge metadata
        var merged = MergedPaperBuilder.MergeFromSources(sources, paper.Doi ?? string.Empty);

        // Update paper fields only when currently empty (never overwrite)
        if (merged != null)
        {
            if (string.IsNullOrWhiteSpace(paper.Abstract) && !string.IsNullOrWhiteSpace(merged.Abstract))
            {
                paper.Abstract = merged.Abstract;
            }
            if (string.IsNullOrWhiteSpace(paper.PdfUrl) && !string.IsNullOrWhiteSpace(merged.PdfUrl))
            {
                paper.PdfUrl = merged.PdfUrl;
            }
            if (paper.PublicationYear == null && merged.Year.HasValue)
            {
                paper.PublicationYear = merged.Year;
            }
            if (paper.CitationCount == 0 && merged.CitationCount.HasValue)
            {
                paper.CitationCount = merged.CitationCount.Value;
            }

            paper.UpdatedAt = DateTime.UtcNow;
        }

        // Upsert PaperSources for newly-fetched sources
        foreach (var (name, src) in sources)
        {
            if (!src.Found) continue;

            var existing = paper.PaperSources.FirstOrDefault(ps => ps.SourceName == name);
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
                    SourceName = name,
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
        _logger.LogInformation("Enriched paper {PaperId} with {Count} sources",
            paperId, sources.Count);
    }

    private async Task SafeFetchAsync(
        string key,
        Func<Task<PaperSourceMetadataDto>> fetch,
        Dictionary<string, PaperSourceMetadataDto> target)
    {
        try
        {
            var dto = await fetch();
            if (dto != null && dto.Found)
            {
                target[key] = dto;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fetch failed for source {Source}", key);
        }
    }
}
