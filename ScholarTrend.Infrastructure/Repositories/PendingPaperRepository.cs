using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

public class PendingPaperRepository : GenericRepository<PendingPaper>, IPendingPaperRepository
{
    public PendingPaperRepository(ScholarTrendDbContext context) : base(context)
    {
    }
}
