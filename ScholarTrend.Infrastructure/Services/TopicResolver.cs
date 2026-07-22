using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Services;

public class TopicResolver : ITopicResolver
{
    private const int MaxNameLength = 200;

    private readonly ScholarTrendDbContext _context;

    public TopicResolver(ScholarTrendDbContext context)
    {
        _context = context;
    }

    public async Task<int?> ResolveAsync(string? topicName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(topicName))
            return null;

        var name = Normalize(topicName);
        if (name.Length == 0)
            return null;

        var existing = await _context.ResearchTopics
            .FirstOrDefaultAsync(t => EF.Functions.ILike(t.TopicName, name), ct);

        if (existing != null)
            return existing.Id;

        var topic = new ResearchTopic
        {
            TopicName = name,
            Description = "Imported from external paper sync."
        };
        await _context.ResearchTopics.AddAsync(topic, ct);
        await _context.SaveChangesAsync(ct);
        return topic.Id;
    }

    private static string Normalize(string raw)
    {
        var collapsed = string.Join(' ',
            raw.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length <= MaxNameLength
            ? collapsed
            : collapsed[..MaxNameLength].Trim();
    }
}
