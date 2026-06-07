using Microsoft.EntityFrameworkCore;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Data.Seeders;

public static class ApiDataSourceSeeder
{
    public static async Task SeedAsync(ScholarTrendDbContext context)
    {
        if (await context.ApiDataSources.AnyAsync())
        {
            return;
        }

        var sources = new[]
        {
            new ApiDataSource
            {
                Name = "SemanticScholar",
                BaseUrl = "https://api.semanticscholar.org/graph/v1",
                IsActive = true
            },
            new ApiDataSource
            {
                Name = "OpenAlex",
                BaseUrl = "https://api.openalex.org",
                IsActive = true
            }
        };

        await context.ApiDataSources.AddRangeAsync(sources);
        await context.SaveChangesAsync();
    }
}
