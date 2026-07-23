using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Services;

public class PaperAuthorLinkerService : IPaperAuthorLinkerService
{
    private readonly ScholarTrendDbContext _context;

    public PaperAuthorLinkerService(ScholarTrendDbContext context)
    {
        _context = context;
    }

    public async Task LinkAuthorsAsync(int paperId, IEnumerable<string> authorNames, CancellationToken ct = default)
    {
        var hasAuthors = await _context.PaperAuthors.AnyAsync(pa => pa.PaperId == paperId, ct);
        if (hasAuthors)
        {
            return;
        }

        var order = 1;
        var addedAuthorIds = new HashSet<int>();

        var distinctNames = authorNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10);

        foreach (var name in distinctNames)
        {
            var author = await _context.Authors
                .FirstOrDefaultAsync(a => a.Name == name, ct);

            if (author == null)
            {
                author = new Author { Name = name };
                await _context.Authors.AddAsync(author, ct);
                await _context.SaveChangesAsync(ct);
            }

            if (addedAuthorIds.Contains(author.Id))
            {
                continue;
            }

            var exists = await _context.PaperAuthors
                .AnyAsync(pa => pa.PaperId == paperId && pa.AuthorId == author.Id, ct);

            if (!exists)
            {
                await _context.PaperAuthors.AddAsync(new PaperAuthor
                {
                    PaperId = paperId,
                    AuthorId = author.Id,
                    AuthorOrder = order++
                }, ct);
                addedAuthorIds.Add(author.Id);
            }
        }

        await _context.SaveChangesAsync(ct);
    }
}
