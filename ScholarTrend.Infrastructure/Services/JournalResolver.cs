using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Services;

public class JournalResolver : IJournalResolver
{
    private readonly ScholarTrendDbContext _context;

    public JournalResolver(ScholarTrendDbContext context)
    {
        _context = context;
    }

    public async Task<int?> ResolveAsync(string? journalName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(journalName))
        {
            return null;
        }

        var name = journalName.Trim();
        var existing = await _context.Journals
            .FirstOrDefaultAsync(j => EF.Functions.ILike(j.Name, name), ct);

        if (existing != null)
        {
            return existing.Id;
        }

        var journal = new Journal { Name = name };
        await _context.Journals.AddAsync(journal, ct);
        await _context.SaveChangesAsync(ct);
        return journal.Id;
    }
}
