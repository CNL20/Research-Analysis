using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;
using ScholarTrend.Infrastructure.Persistence.Repositories;

namespace ScholarTrend.Infrastructure.Repositories;

public class PaperQualityRepository : IPaperQualityRepository
{
    private readonly DbSet<PaperQuality> _db;

    public PaperQualityRepository(ScholarTrendDbContext context)
    {
        _db = context.Set<PaperQuality>();
    }

    public async Task<PaperQuality?> GetByIdAsync(int id) => await _db.FindAsync(id);

    public async Task<PaperQuality?> GetByPaperIdAsync(int paperId)
    {
        return await _db.FirstOrDefaultAsync(x => x.PaperId == paperId);
    }

    public async Task<List<PaperQuality>> GetByTopicIdAsync(int topicId)
    {
        return await _db
            .Include(x => x.Paper)
            .ThenInclude(p => p.PaperTopics)
            .Where(x => x.Paper.PaperTopics.Any(pt => pt.TopicId == topicId))
            .ToListAsync();
    }

    public async Task<List<PaperQuality>> GetByAnalysisLevelAsync(string level, int take = 100)
    {
        return await _db
            .Where(x => x.AnalysisLevel == level)
            .Take(take)
            .ToListAsync();
    }

    public async Task<PaperQuality> UpsertAsync(PaperQuality quality)
    {
        var existing = await GetByPaperIdAsync(quality.PaperId);
        if (existing != null)
        {
            existing.HasPdf = quality.HasPdf;
            existing.HasAbstract = quality.HasAbstract;
            existing.AbstractLength = quality.AbstractLength;
            existing.AuthorCount = quality.AuthorCount;
            existing.HasDoi = quality.HasDoi;
            existing.HasKeywords = quality.HasKeywords;
            existing.HasJournal = quality.HasJournal;
            existing.CitationCount = quality.CitationCount;
            existing.QualityScore = quality.QualityScore;
            existing.QualityGrade = quality.QualityGrade;
            existing.AnalysisLevel = quality.AnalysisLevel;
            existing.AssessedAt = quality.AssessedAt;
            return existing;
        }
        
        await _db.AddAsync(quality);
        return quality;
    }

    public async Task AddAsync(PaperQuality quality)
    {
        await _db.AddAsync(quality);
    }

    public void Update(PaperQuality quality)
    {
        _db.Update(quality);
    }
}
