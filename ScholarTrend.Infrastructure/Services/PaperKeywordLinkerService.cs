using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Logging;

using ScholarTrend.Application.Interfaces;

using ScholarTrend.Application.Services.Keywords;

using ScholarTrend.Domain.Entities;

using ScholarTrend.Infrastructure.Data;



namespace ScholarTrend.Infrastructure.Services;



public class PaperKeywordLinkerService : IPaperKeywordLinkerService

{

    private readonly ScholarTrendDbContext _context;

    private readonly ILogger<PaperKeywordLinkerService> _logger;



    public PaperKeywordLinkerService(

        ScholarTrendDbContext context,

        ILogger<PaperKeywordLinkerService> logger)

    {

        _context = context;

        _logger = logger;

    }



    public Task LinkKeywordsAsync(

        int paperId,

        IEnumerable<string> keywordNames,

        CancellationToken ct = default)

    {

        var prepared = KeywordLinkRules.PrepareKeywordNames(keywordNames);

        return LinkPreparedKeywordsAsync(paperId, prepared, ct);

    }



    public async Task LinkFromContextAsync(

        int paperId,

        string? title,

        string? abstractText,

        string? syncSearchQuery,

        IEnumerable<string>? apiKeywords,

        CancellationToken ct = default)

    {

        var text = $"{title ?? string.Empty} {abstractText ?? string.Empty}";

        var seedMatches = KeywordLinkRules.MatchSeedsFromText(text);

        if (seedMatches.Count > 0)

        {

            await LinkPreparedKeywordsAsync(paperId, seedMatches, ct);

        }



        if (apiKeywords != null)

        {

            await LinkKeywordsAsync(paperId, apiKeywords, ct);

        }



        if (!string.IsNullOrWhiteSpace(syncSearchQuery))

        {

            await LinkKeywordsAsync(paperId, [syncSearchQuery], ct);

        }

    }



    private async Task LinkPreparedKeywordsAsync(

        int paperId,

        IReadOnlyList<string> displayNames,

        CancellationToken ct)

    {

        if (displayNames.Count == 0)

        {

            return;

        }



        var existingCount = await _context.PaperKeywords

            .CountAsync(pk => pk.PaperId == paperId, ct);



        if (existingCount >= KeywordLinkRules.MaxKeywordsPerPaper)

        {

            return;

        }



        var linkedKeywordIds = await _context.PaperKeywords

            .Where(pk => pk.PaperId == paperId)

            .Select(pk => pk.KeywordId)

            .ToListAsync(ct);



        var linkedNames = await _context.Keywords

            .Where(k => linkedKeywordIds.Contains(k.Id))

            .Select(k => k.Name)

            .ToListAsync(ct);



        var linkedNameSet = new HashSet<string>(linkedNames, StringComparer.OrdinalIgnoreCase);

        var added = 0;



        foreach (var displayName in displayNames)

        {

            if (existingCount + added >= KeywordLinkRules.MaxKeywordsPerPaper)

            {

                break;

            }



            if (linkedNameSet.Contains(displayName))

            {

                continue;

            }



            var keyword = await _context.Keywords

                .FirstOrDefaultAsync(k => EF.Functions.ILike(k.Name, displayName), ct);



            if (keyword == null)

            {

                keyword = new Keyword { Name = displayName };

                await _context.Keywords.AddAsync(keyword, ct);

                await _context.SaveChangesAsync(ct);

            }



            var alreadyLinked = await _context.PaperKeywords

                .AnyAsync(pk => pk.PaperId == paperId && pk.KeywordId == keyword.Id, ct);



            if (alreadyLinked)

            {

                continue;

            }



            await _context.PaperKeywords.AddAsync(new PaperKeyword

            {

                PaperId = paperId,

                KeywordId = keyword.Id

            }, ct);



            linkedNameSet.Add(displayName);

            added++;

        }



        if (added > 0)

        {

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(

                "Linked {Count} keyword(s) to paper {PaperId}",

                added, paperId);

        }

    }

}

