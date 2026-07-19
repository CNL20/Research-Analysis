using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

public class PatternRepository : IPatternRepository
{
    private readonly DbSet<MethodPattern> _methodDb;
    private readonly DbSet<DatasetPattern> _datasetDb;
    private readonly DbSet<LimitationPattern> _limitationDb;

    public PatternRepository(ScholarTrendDbContext context)
    {
        _methodDb = context.Set<MethodPattern>();
        _datasetDb = context.Set<DatasetPattern>();
        _limitationDb = context.Set<LimitationPattern>();
    }

    public async Task<List<MethodPattern>> GetMethodPatternsAsync(int topicId, int? yearFrom = null, int? yearTo = null)
    {
        var query = _methodDb.Where(x => x.TopicId == topicId);
        if (yearFrom.HasValue) query = query.Where(x => x.Year >= yearFrom.Value);
        if (yearTo.HasValue) query = query.Where(x => x.Year <= yearTo.Value);
        return await query.OrderByDescending(x => x.PaperCount).ToListAsync();
    }

    public async Task<List<DatasetPattern>> GetDatasetPatternsAsync(int topicId, int? yearFrom = null, int? yearTo = null)
    {
        var query = _datasetDb.Where(x => x.TopicId == topicId);
        if (yearFrom.HasValue) query = query.Where(x => x.Year >= yearFrom.Value);
        if (yearTo.HasValue) query = query.Where(x => x.Year <= yearTo.Value);
        return await query.OrderByDescending(x => x.PaperCount).ToListAsync();
    }

    public async Task<List<LimitationPattern>> GetLimitationPatternsAsync(int topicId, int? yearFrom = null, int? yearTo = null)
    {
        var query = _limitationDb.Where(x => x.TopicId == topicId);
        if (yearFrom.HasValue) query = query.Where(x => x.Year >= yearFrom.Value);
        if (yearTo.HasValue) query = query.Where(x => x.Year <= yearTo.Value);
        return await query.OrderByDescending(x => x.PaperCount).ToListAsync();
    }

    public async Task UpsertMethodPatternsAsync(List<MethodPattern> patterns)
    {
        foreach (var pattern in patterns)
        {
            var existing = await _methodDb.FirstOrDefaultAsync(x =>
                x.TopicId == pattern.TopicId &&
                x.MethodName == pattern.MethodName &&
                x.Year == pattern.Year);

            if (existing != null)
            {
                existing.PaperCount = pattern.PaperCount;
                existing.GrowthRate = pattern.GrowthRate;
                existing.MinedAt = pattern.MinedAt;
            }
            else
            {
                await _methodDb.AddAsync(pattern);
            }
        }
    }

    public async Task UpsertDatasetPatternsAsync(List<DatasetPattern> patterns)
    {
        foreach (var pattern in patterns)
        {
            var existing = await _datasetDb.FirstOrDefaultAsync(x =>
                x.TopicId == pattern.TopicId &&
                x.DatasetName == pattern.DatasetName &&
                x.Year == pattern.Year);

            if (existing != null)
            {
                existing.PaperCount = pattern.PaperCount;
                existing.GrowthRate = pattern.GrowthRate;
                existing.MinedAt = pattern.MinedAt;
            }
            else
            {
                await _datasetDb.AddAsync(pattern);
            }
        }
    }

    public async Task UpsertLimitationPatternsAsync(List<LimitationPattern> patterns)
    {
        foreach (var pattern in patterns)
        {
            var existing = await _limitationDb.FirstOrDefaultAsync(x =>
                x.TopicId == pattern.TopicId &&
                x.LimitationText == pattern.LimitationText &&
                x.Year == pattern.Year);

            if (existing != null)
            {
                existing.PaperCount = pattern.PaperCount;
                existing.GrowthRate = pattern.GrowthRate;
                existing.MinedAt = pattern.MinedAt;
            }
            else
            {
                await _limitationDb.AddAsync(pattern);
            }
        }
    }

    public async Task DeleteByTopicAsync(int topicId)
    {
        await _methodDb.Where(x => x.TopicId == topicId).ExecuteDeleteAsync();
        await _datasetDb.Where(x => x.TopicId == topicId).ExecuteDeleteAsync();
        await _limitationDb.Where(x => x.TopicId == topicId).ExecuteDeleteAsync();
    }
}
