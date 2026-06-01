using Microsoft.EntityFrameworkCore;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Data.Seeders;

public static class KeywordSeeder
{
    public static async Task<List<Keyword>> SeedAsync(ScholarTrendDbContext context)
    {
        if (await context.Keywords.AnyAsync())
        {
            return await context.Keywords.OrderBy(k => k.Id).ToListAsync();
        }

        var keywords = new List<Keyword>
        {
            new() { Name = "Artificial Intelligence" },
            new() { Name = "Machine Learning" },
            new() { Name = "Deep Learning" },
            new() { Name = "Data Mining" },
            new() { Name = "Computer Vision" },
            new() { Name = "Natural Language Processing" },
            new() { Name = "Blockchain" },
            new() { Name = "Cybersecurity" },
            new() { Name = "Big Data" },
            new() { Name = "Internet of Things" }
        };

        await context.Keywords.AddRangeAsync(keywords);
        await context.SaveChangesAsync();
        return keywords;
    }
}
