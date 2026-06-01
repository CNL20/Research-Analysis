using Microsoft.EntityFrameworkCore;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Data.Seeders;

public static class JournalSeeder
{
    public static async Task<List<Journal>> SeedAsync(ScholarTrendDbContext context)
    {
        if (await context.Journals.AnyAsync())
        {
            return await context.Journals.OrderBy(j => j.Id).ToListAsync();
        }

        var journals = new List<Journal>
        {
            new()
            {
                Name = "Nature",
                Publisher = "Springer Nature",
                Issn = "1476-4687",
                Website = "https://www.nature.com",
                ImpactFactor = 64.8,
                HIndex = 1000
            },
            new()
            {
                Name = "Science",
                Publisher = "AAAS",
                Issn = "0036-8075",
                Website = "https://www.science.org",
                ImpactFactor = 56.9,
                HIndex = 980
            },
            new()
            {
                Name = "IEEE Access",
                Publisher = "IEEE",
                Issn = "2169-3536",
                Website = "https://ieeexplore.ieee.org/xpl/RecentIssue.jsp?punumber=6287639",
                ImpactFactor = 3.9,
                HIndex = 210
            },
            new()
            {
                Name = "ACM Computing Surveys",
                Publisher = "ACM",
                Issn = "0360-0300",
                Website = "https://dl.acm.org/journal/csur",
                ImpactFactor = 23.8,
                HIndex = 260
            },
            new()
            {
                Name = "Artificial Intelligence Journal",
                Publisher = "Elsevier",
                Issn = "0004-3702",
                Website = "https://www.sciencedirect.com/journal/artificial-intelligence",
                ImpactFactor = 14.0,
                HIndex = 180
            }
        };

        await context.Journals.AddRangeAsync(journals);
        await context.SaveChangesAsync();
        return journals;
    }
}
