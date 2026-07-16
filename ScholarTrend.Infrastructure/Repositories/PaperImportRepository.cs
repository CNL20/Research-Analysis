using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Application.Services.Aggregation;
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
    private readonly IPaperAuthorLinkerService _authorLinker;
    private readonly ILogger<PaperImportRepository> _logger;

    public PaperImportRepository(
        ScholarTrendDbContext context,
        IArxivDoiResolver arxivDoi,
        IEnrichPaperSourcesEnqueuer enrichEnqueuer,
        IPaperKeywordLinkerService keywordLinker,
        IJournalResolver journalResolver,
        IPaperAuthorLinkerService authorLinker,
        ILogger<PaperImportRepository> logger)
    {
        _context = context;
        _arxivDoi = arxivDoi;
        _enrichEnqueuer = enrichEnqueuer;
        _keywordLinker = keywordLinker;
        _journalResolver = journalResolver;
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

        var linkedKeywordNames = await _context.PaperKeywords
            .Where(pk => pk.PaperId == paperId)
            .Select(pk => pk.Keyword.Name)
            .ToListAsync(ct);

        var text = $"{external.Title ?? string.Empty} {external.Abstract ?? string.Empty}"
            .ToLowerInvariant();

        ResearchTopic? topic = null;
        if (linkedKeywordNames.Count > 0)
        {
            var topics = await _context.ResearchTopics.ToListAsync(ct);
            topic = topics.FirstOrDefault(t =>
                linkedKeywordNames.Any(k =>
                    t.TopicName.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                    k.Contains(t.TopicName, StringComparison.OrdinalIgnoreCase)));
        }

        if (topic == null)
        {
            topic = await MatchTopicByKeywordsAsync(text, ct);
        }

        if (topic != null)
        {
            var topicExists = await _context.PaperTopics
                .AnyAsync(pt => pt.PaperId == paperId && pt.TopicId == topic.Id, ct);
            if (!topicExists)
            {
                await _context.PaperTopics.AddAsync(new PaperTopic
                {
                    PaperId = paperId,
                    TopicId = topic.Id
                }, ct);
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    private async Task<ResearchTopic?> MatchTopicByKeywordsAsync(string text, CancellationToken ct)
    {
        var mlPatterns = new[] { "machine learning", "deep learning", "neural network", "artificial intelligence",
            "transformer", "bert", "gpt", "lstm", "reinforcement learning", "supervised learning", "unsupervised learning" };
        var mlSingleKeywords = new[] { "cnn", "rnn", "gan", "vae", "mlp" };

        var cvPatterns = new[] { "computer vision", "image recognition", "object detection", "image segmentation" };
        var cvSingleKeywords = new[] { "yolo", "resnet", "faster r-cnn" };

        var nlpPatterns = new[] { "natural language", "sentiment analysis", "machine translation", "language model", "text classification" };
        var nlpSingleKeywords = new[] { "nlp", "llm" };

        var roboticsPatterns = new[] { "robotics", "autonomous", "motion planning", "robot control" };
        var roboticsSingleKeywords = new[] { "kinematics", "manipulator" };

        if (mlPatterns.Any(p => text.Contains(p)))
            return await GetTopicByNameAsync("Machine Learning", ct);
        if (cvPatterns.Any(p => text.Contains(p)))
            return await GetTopicByNameAsync("Computer Vision", ct);
        if (nlpPatterns.Any(p => text.Contains(p)))
            return await GetTopicByNameAsync("NLP", ct);
        if (roboticsPatterns.Any(p => text.Contains(p)))
            return await GetTopicByNameAsync("Robotics", ct);

        if (mlSingleKeywords.Any(k => HasWordBoundary(text, k)))
            return await GetTopicByNameAsync("Machine Learning", ct);
        if (cvSingleKeywords.Any(k => HasWordBoundary(text, k)))
            return await GetTopicByNameAsync("Computer Vision", ct);
        if (nlpSingleKeywords.Any(k => HasWordBoundary(text, k)))
            return await GetTopicByNameAsync("NLP", ct);
        if (roboticsSingleKeywords.Any(k => HasWordBoundary(text, k)))
            return await GetTopicByNameAsync("Robotics", ct);

        return null;
    }

    private async Task<ResearchTopic?> GetTopicByNameAsync(string topicName, CancellationToken ct)
    {
        return await _context.ResearchTopics
            .FirstOrDefaultAsync(t => EF.Functions.ILike(t.TopicName, topicName), ct);
    }

    private static bool HasWordBoundary(string text, string keyword)
    {
        return text.Contains($" {keyword} ") ||
               text.Contains($" {keyword},") ||
               text.Contains($" {keyword}.") ||
               text.Contains($" {keyword}?") ||
               text.Contains($" {keyword}!") ||
               text.Contains($" {keyword};") ||
               text.Contains($" {keyword}:") ||
               text.Contains($" {keyword}-") ||
               text.Contains($"({keyword} ") ||
               text.StartsWith($"{keyword} ") ||
               text.EndsWith($" {keyword}");
    }

    private static string ExtractArxivId(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var match = ArxivIdRegex.Match(raw);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }
}
