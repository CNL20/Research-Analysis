using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;
using System.Text.Json;

namespace ScholarTrend.Infrastructure.Repositories;

public class PaperAnalysisRepository : IPaperAnalysisRepository
{
    private readonly DbSet<PaperAnalysis> _db;

    public PaperAnalysisRepository(ScholarTrendDbContext context)
    {
        _db = context.Set<PaperAnalysis>();
    }

    public async Task<PaperAnalysis?> GetByIdAsync(int id) => await _db.FindAsync(id);

    public async Task<PaperAnalysis?> GetByPaperIdAsync(int paperId)
    {
        return await _db.FirstOrDefaultAsync(x => x.PaperId == paperId);
    }

    public async Task<List<PaperAnalysis>> GetByTopicIdAsync(int topicId)
    {
        return await _db
            .Include(x => x.Paper)
            .ThenInclude(p => p.PaperTopics)
            .Where(x => x.Paper.PaperTopics.Any(pt => pt.TopicId == topicId))
            .ToListAsync();
    }

    public async Task<List<PaperAnalysis>> GetByTopicIdWithLimitAsync(int topicId, int limit)
    {
        return await _db
            .Include(x => x.Paper)
            .ThenInclude(p => p.PaperTopics)
            .Where(x => x.Paper.PaperTopics.Any(pt => pt.TopicId == topicId))
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<PaperAnalysis>> GetByPaperIdsAsync(IEnumerable<int> paperIds)
    {
        var ids = paperIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        return await _db
            .Include(x => x.Paper)
            .Where(x => ids.Contains(x.PaperId))
            .ToListAsync();
    }

    public async Task<List<PaperAnalysis>> GetAnalyzedPapersWithoutFullTextAsync(int topicId, int take = 50)
    {
        return await _db
            .Include(x => x.Paper)
            .ThenInclude(p => p.PaperTopics)
            .Where(x => x.Paper.PaperTopics.Any(pt => pt.TopicId == topicId))
            .Where(x => x.AnalysisLevel == AnalysisLevels.Abstract && x.Paper.PdfUrl != null)
            .OrderByDescending(x => x.Paper.CitationCount)
            .Take(take)
            .ToListAsync();
    }

    public async Task<PaperAnalysis> UpsertAsync(PaperAnalysis analysis)
    {
        var existing = await GetByPaperIdAsync(analysis.PaperId);
        if (existing != null)
        {
            existing.ResearchProblem = analysis.ResearchProblem;
            existing.Method = analysis.Method;
            existing.Dataset = analysis.Dataset;
            existing.Metric = analysis.Metric;
            existing.Contribution = analysis.Contribution;
            existing.MethodsJson = analysis.MethodsJson;
            existing.DatasetsJson = analysis.DatasetsJson;
            existing.LimitationsJson = analysis.LimitationsJson;
            existing.FutureWorkJson = analysis.FutureWorkJson;
            existing.DiscussionsJson = analysis.DiscussionsJson;
            existing.ConclusionsJson = analysis.ConclusionsJson;
            existing.KeywordsJson = analysis.KeywordsJson;
            existing.EvidenceSentence = analysis.EvidenceSentence;
            existing.Confidence = analysis.Confidence;
            existing.AnalysisLevel = analysis.AnalysisLevel;
            existing.AnalysisSource = analysis.AnalysisSource;
            existing.UpdatedAt = DateTime.UtcNow;
            return existing;
        }
        
        await _db.AddAsync(analysis);
        return analysis;
    }

    public async Task AddAsync(PaperAnalysis analysis)
    {
        await _db.AddAsync(analysis);
    }

    public void Update(PaperAnalysis analysis)
    {
        _db.Update(analysis);
    }
}
