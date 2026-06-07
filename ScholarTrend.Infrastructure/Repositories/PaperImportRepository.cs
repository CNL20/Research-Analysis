using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Domain.Enums;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

public class PaperImportRepository : IPaperImportRepository
{
    private readonly ScholarTrendDbContext _context;

    public PaperImportRepository(ScholarTrendDbContext context)
    {
        _context = context;
    }

    public async Task<ResearchPaperImportResult> ImportAsync(ExternalPaperDto external, int? journalId)
    {
        var existing = await _context.ResearchPapers
            .FirstOrDefaultAsync(p => p.ExternalId == external.ExternalId && p.ExternalSource == external.Source);

        if (existing != null)
        {
            existing.CitationCount = external.CitationCount;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.Status = PaperStatus.Updated;
            _context.ResearchPapers.Update(existing);
            await _context.SaveChangesAsync();
            return new ResearchPaperImportResult { PaperId = existing.Id, IsNew = false };
        }

        var paper = new ResearchPaper
        {
            Title = external.Title,
            Abstract = external.Abstract,
            PublicationYear = external.Year,
            PublicationDate = external.Year.HasValue ? new DateTime(external.Year.Value, 6, 1) : null,
            CitationCount = external.CitationCount,
            Doi = external.Doi,
            Url = external.Url,
            ExternalId = external.ExternalId,
            ExternalSource = external.Source,
            JournalId = journalId,
            Status = PaperStatus.Available,
            CreatedAt = DateTime.UtcNow
        };

        await _context.ResearchPapers.AddAsync(paper);
        await _context.SaveChangesAsync();

        await LinkAuthorsAsync(paper.Id, external.AuthorNames);
        await LinkKeywordsAndTopicAsync(paper.Id, external);

        return new ResearchPaperImportResult { PaperId = paper.Id, IsNew = true };
    }

    private async Task LinkAuthorsAsync(int paperId, List<string> authorNames)
    {
        var order = 1;
        foreach (var name in authorNames.Take(5))
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var author = await _context.Authors.FirstOrDefaultAsync(a => a.Name == name);
            if (author == null)
            {
                author = new Author { Name = name };
                await _context.Authors.AddAsync(author);
                await _context.SaveChangesAsync();
            }

            var exists = await _context.PaperAuthors.AnyAsync(pa => pa.PaperId == paperId && pa.AuthorId == author.Id);
            if (!exists)
            {
                await _context.PaperAuthors.AddAsync(new PaperAuthor
                {
                    PaperId = paperId,
                    AuthorId = author.Id,
                    AuthorOrder = order++
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task LinkKeywordsAndTopicAsync(int paperId, ExternalPaperDto external)
    {
        var keywords = await _context.Keywords.ToListAsync();
        var text = $"{external.Title} {external.Abstract}".ToLowerInvariant();
        var matched = keywords.Where(k => text.Contains(k.Name.ToLowerInvariant())).Take(3).ToList();

        foreach (var keyword in matched)
        {
            var exists = await _context.PaperKeywords.AnyAsync(pk => pk.PaperId == paperId && pk.KeywordId == keyword.Id);
            if (!exists)
            {
                await _context.PaperKeywords.AddAsync(new PaperKeyword { PaperId = paperId, KeywordId = keyword.Id });
            }
        }

        var topic = matched.Count > 0
            ? await _context.ResearchTopics.FirstOrDefaultAsync(t =>
                matched.Any(k => t.TopicName.Contains(k.Name, StringComparison.OrdinalIgnoreCase)))
            : await _context.ResearchTopics.FirstOrDefaultAsync();

        topic ??= await _context.ResearchTopics.FirstOrDefaultAsync();

        if (topic != null)
        {
            var topicExists = await _context.PaperTopics.AnyAsync(pt => pt.PaperId == paperId && pt.TopicId == topic.Id);
            if (!topicExists)
            {
                await _context.PaperTopics.AddAsync(new PaperTopic { PaperId = paperId, TopicId = topic.Id });
            }
        }

        await _context.SaveChangesAsync();
    }
}
