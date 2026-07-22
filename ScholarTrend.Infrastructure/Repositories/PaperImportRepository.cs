using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Application.Services.Aggregation;
using ScholarTrend.Application.Services.Topics;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Domain.Enums;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

/// <summary>
/// Imports an <see cref="ExternalPaperDto"/> into the local DB with cross-source dedup.
///
/// Resolution order:
/// 1. By DOI (any paper that already has a PaperSources row with that DOI).
/// 2. By ArXiv ID (only when the incoming paper is from ArXiv and no DOI was found yet).
///
/// Merge policy (see <see cref="MergedPaperBuilder"/>):
/// Crossref > OpenAlex > SemanticScholar > ArXiv.
/// </summary>
public class PaperImportRepository : IPaperImportRepository
{
    private static readonly Regex ArxivIdRegex = new(@"(\d{4}\.\d{4,5})(v\d+)?", RegexOptions.Compiled);

    private readonly ScholarTrendDbContext _context;
    private readonly IArxivDoiResolver _arxivDoi;
    private readonly IEnrichPaperSourcesEnqueuer _enrichEnqueuer;
    private readonly IPaperKeywordLinkerService _keywordLinker;
    private readonly IJournalResolver _journalResolver;
    private readonly ITopicResolver _topicResolver;
    private readonly IPaperAuthorLinkerService _authorLinker;
    private readonly ILogger<PaperImportRepository> _logger;

    public PaperImportRepository(
        ScholarTrendDbContext context,
        IArxivDoiResolver arxivDoi,
        IEnrichPaperSourcesEnqueuer enrichEnqueuer,
        IPaperKeywordLinkerService keywordLinker,
        IJournalResolver journalResolver,
        ITopicResolver topicResolver,
        IPaperAuthorLinkerService authorLinker,
        ILogger<PaperImportRepository> logger)
    {
        _context = context;
        _arxivDoi = arxivDoi;
        _enrichEnqueuer = enrichEnqueuer;
        _keywordLinker = keywordLinker;
        _journalResolver = journalResolver;
        _topicResolver = topicResolver;
        _authorLinker = authorLinker;
        _logger = logger;
    }

    public async Task<ResearchPaperImportResult> ImportAsync(
        ExternalPaperDto external,
        int? journalId = null,
        CancellationToken ct = default)
    {
        // ============ STEP 1: Resolve canonical DOI ============
        var canonicalDoi = !string.IsNullOrWhiteSpace(external.Doi)
            ? MetadataMapper.NormalizeDoi(external.Doi)
            : null;

        if (string.IsNullOrEmpty(canonicalDoi) && external.Source == "ArXiv")
        {
            try
            {
                canonicalDoi = await _arxivDoi.ResolveDoiAsync(external.ExternalId, ct);
                if (!string.IsNullOrEmpty(canonicalDoi))
                {
                    _logger.LogInformation(
                        "Resolved ArXiv {ArxivId} -> DOI {Doi}", external.ExternalId, canonicalDoi);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ArXiv DOI lookup failed for {ArxivId}", external.ExternalId);
            }
        }

        // ============ STEP 2: Find existing paper (via PaperSources) ============
        ResearchPaper? paper = null;

        if (!string.IsNullOrEmpty(canonicalDoi))
        {
            paper = await _context.ResearchPapers
                .Include(p => p.PaperSources)
                .FirstOrDefaultAsync(p =>
                    p.PaperSources.Any(ps => ps.SourceDoi == canonicalDoi), ct);
        }

        if (paper == null && external.Source == "ArXiv")
        {
            var arxivId = ExtractArxivId(external.ExternalId);
            if (!string.IsNullOrEmpty(arxivId))
            {
                paper = await _context.ResearchPapers
                    .Include(p => p.PaperSources)
                    .FirstOrDefaultAsync(p =>
                        p.PaperSources.Any(ps =>
                            ps.SourceName == "ArXiv" && ps.ExternalId == arxivId), ct);
            }
        }

        // ============ STEP 3A: Insert new ============
        if (paper == null)
        {
            var resolvedJournalId = await ResolveJournalIdAsync(external.Journal, journalId, ct);

            paper = new ResearchPaper
            {
                Title = external.Title ?? "(no title)",
                Abstract = external.Abstract,
                PublicationYear = external.Year,
                PublicationDate = external.Year.HasValue
                    ? new DateTime(external.Year.Value, 6, 1, 0, 0, 0, DateTimeKind.Utc)
                    : null,
                Doi = !string.IsNullOrEmpty(canonicalDoi) ? canonicalDoi : null,
                Url = external.Url,
                PdfUrl = external.PdfUrl,
                CitationCount = external.CitationCount ?? 0,
                JournalId = resolvedJournalId,
                Status = PaperStatus.Available,
                CreatedAt = DateTime.UtcNow,
                PaperSources = new List<PaperSource>
                {
                    new()
                    {
                        SourceName = external.Source,
                        ExternalId = external.ExternalId,
                        SourceDoi = !string.IsNullOrEmpty(canonicalDoi) ? canonicalDoi : null,
                        SourceUrl = external.Url,
                        SourceCitationCount = external.CitationCount,
                        SourceYear = external.Year,
                        FetchedAt = DateTime.UtcNow,
                        LastSeenAt = DateTime.UtcNow,
                        RawMetadataJson = SerializeSourceMetadata(external)
                    }
                }
            };

            await _context.ResearchPapers.AddAsync(paper, ct);
            await _context.SaveChangesAsync(ct);

            await _authorLinker.LinkAuthorsAsync(paper.Id, external.AuthorNames, ct);
            await LinkKeywordsAndTopicAsync(paper.Id, external, ct);

            // Q5: Enqueue background enrichment to fill in other sources.
            try
            {
                await _enrichEnqueuer.EnqueueEnrichmentAsync(
                    paper.Id, canonicalDoi, external.Source, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enqueue enrichment for paper {PaperId}", paper.Id);
            }

            return new ResearchPaperImportResult { PaperId = paper.Id, IsNew = true };
        }

        // ============ STEP 3B: Update existing ============
        // Q6: Update DOI if currently missing.
        if (string.IsNullOrEmpty(paper.Doi) && !string.IsNullOrEmpty(canonicalDoi))
        {
            paper.Doi = canonicalDoi;
        }

        paper.CitationCount = Math.Max(paper.CitationCount ?? 0, external.CitationCount ?? 0);

        if (string.IsNullOrEmpty(paper.PdfUrl) && !string.IsNullOrEmpty(external.PdfUrl))
        {
            paper.PdfUrl = external.PdfUrl;
        }

        if (string.IsNullOrEmpty(paper.Abstract) && !string.IsNullOrEmpty(external.Abstract))
        {
            paper.Abstract = external.Abstract;
        }

        paper.UpdatedAt = DateTime.UtcNow;
        paper.Status = PaperStatus.Updated;

        // Upsert PaperSource row for this source
        var existingSource = paper.PaperSources
            .FirstOrDefault(ps => ps.SourceName == external.Source);

        if (existingSource != null)
        {
            existingSource.LastSeenAt = DateTime.UtcNow;
            existingSource.SourceCitationCount = external.CitationCount;
            existingSource.RawMetadataJson = SerializeSourceMetadata(external);
        }
        else
        {
            paper.PaperSources.Add(new PaperSource
            {
                SourceName = external.Source,
                ExternalId = external.ExternalId,
                SourceDoi = !string.IsNullOrEmpty(canonicalDoi) ? canonicalDoi : null,
                SourceUrl = external.Url,
                SourceCitationCount = external.CitationCount,
                SourceYear = external.Year,
                FetchedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                RawMetadataJson = SerializeSourceMetadata(external)
            });
        }

        if (paper.JournalId == null && !string.IsNullOrWhiteSpace(external.Journal))
        {
            paper.JournalId = await ResolveJournalIdAsync(external.Journal, null, ct);
        }

        await _context.SaveChangesAsync(ct);
        await _authorLinker.LinkAuthorsAsync(paper.Id, external.AuthorNames, ct);
        await LinkKeywordsAndTopicAsync(paper.Id, external, ct);

        return new ResearchPaperImportResult { PaperId = paper.Id, IsNew = false };
    }

    private async Task<int?> ResolveJournalIdAsync(string? journalName, int? fallbackJournalId, CancellationToken ct)
    {
        var resolved = await _journalResolver.ResolveAsync(journalName, ct);
        if (resolved.HasValue)
        {
            return resolved.Value;
        }

        if (fallbackJournalId.HasValue)
        {
            return fallbackJournalId;
        }

        return null;
    }

    private static string SerializeSourceMetadata(ExternalPaperDto external)
    {
        var sourceKey = SourceNameNormalizer.ToMergeKey(external.Source);
        return System.Text.Json.JsonSerializer.Serialize(MetadataMapper.FromExternal(external, sourceKey));
    }

    private async Task LinkKeywordsAndTopicAsync(int paperId, ExternalPaperDto external, CancellationToken ct)
    {
        await _keywordLinker.LinkFromContextAsync(
            paperId,
            external.Title,
            external.Abstract,
            external.SyncSearchQuery,
            external.Keywords,
            ct);

        var topicIds = new HashSet<int>();

        // 1) Prefer mapping free labels / title onto the 5 seeded topics.
        var seededNames = ScholarTopicMapper.MapToSeededTopics(
            external.Topics,
            external.Title,
            external.Abstract);

        if (seededNames.Count == 0)
        {
            var linkedKeywordNames = await _context.PaperKeywords
                .Where(pk => pk.PaperId == paperId)
                .Select(pk => pk.Keyword.Name)
                .ToListAsync(ct);

            seededNames = ScholarTopicMapper.MapToSeededTopics(
                linkedKeywordNames,
                external.Title,
                external.Abstract);
        }

        if (seededNames.Count > 0)
        {
            var seeded = await _context.ResearchTopics
                .Where(t => seededNames.Contains(t.TopicName))
                .Select(t => t.Id)
                .ToListAsync(ct);
            foreach (var id in seeded)
                topicIds.Add(id);
        }

        // 2) Unmapped API labels → find-or-create ResearchTopic (same idea as JournalResolver).
        foreach (var label in external.Topics ?? [])
        {
            if (string.IsNullOrWhiteSpace(label))
                continue;

            // Already covered by a seeded bucket — do not also create a duplicate free-text topic.
            if (ScholarTopicMapper.MatchOne(label) != null)
                continue;

            var id = await _topicResolver.ResolveAsync(label, ct);
            if (id.HasValue)
                topicIds.Add(id.Value);
        }

        foreach (var topicId in topicIds)
        {
            var topicExists = await _context.PaperTopics
                .AnyAsync(pt => pt.PaperId == paperId && pt.TopicId == topicId, ct);
            if (!topicExists)
            {
                await _context.PaperTopics.AddAsync(new PaperTopic
                {
                    PaperId = paperId,
                    TopicId = topicId
                }, ct);
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    private static string ExtractArxivId(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var match = ArxivIdRegex.Match(raw);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }
}
