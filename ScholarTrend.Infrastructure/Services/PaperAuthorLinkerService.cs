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
        foreach (var name in authorNames.Where(n => !string.IsNullOrWhiteSpace(n)).Take(10))
        {
            var trimmed = name.Trim();
            var author = await _context.Authors
                .FirstOrDefaultAsync(a => a.Name == trimmed, ct);

            if (author == null)
            {
                author = new Author { Name = trimmed };
                await _context.Authors.AddAsync(author, ct);
                await _context.SaveChangesAsync(ct);
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
            }
        }

        await _context.SaveChangesAsync(ct);
    }
}
