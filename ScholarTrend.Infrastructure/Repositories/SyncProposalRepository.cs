using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Constants;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

public class SyncProposalRepository : ISyncProposalRepository
{
    private readonly ScholarTrendDbContext _context;

    public SyncProposalRepository(ScholarTrendDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SyncProposal proposal)
    {
        await _context.SyncProposals.AddAsync(proposal);
    }

    public Task<SyncProposal?> GetByIdWithPapersAsync(int id)
    {
        return _context.SyncProposals
            .Include(p => p.PendingPapers)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IReadOnlyList<SyncProposal>> GetPendingProposalsAsync(int limit = 50)
    {
        return await _context.SyncProposals
            .Include(p => p.PendingPapers)
            .Where(p => p.Status == SyncProposalStatus.Pending || p.Status == SyncProposalStatus.PartiallyApproved)
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public void Update(SyncProposal proposal)
    {
        _context.SyncProposals.Update(proposal);
    }

    public async Task<bool> IsPaperAlreadyQueuedOrStoredAsync(string externalId, string externalSource)
    {
        var existsInPapers = await _context.ResearchPapers
            .AnyAsync(p => p.ExternalId == externalId && p.ExternalSource == externalSource);

        if (existsInPapers)
        {
            return true;
        }

        return await _context.PendingPapers.AnyAsync(p =>
            p.ExternalId == externalId &&
            p.ExternalSource == externalSource &&
            (p.Status == PendingPaperStatus.Pending || p.Status == PendingPaperStatus.Approved));
    }
}