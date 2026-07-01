using Microsoft.EntityFrameworkCore;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Data.Seeders;

public static class ApiDataSourceSeeder
{
    private static readonly (string Name, string BaseUrl)[] DefaultSources =
    [
        ("SemanticScholar", "https://api.semanticscholar.org/graph/v1"),
        ("OpenAlex", "https://api.openalex.org"),
        ("Crossref", "https://api.crossref.org"),
        ("ArXiv", "https://export.arxiv.org/api/query")
    ];

    public static async Task SeedAsync(ScholarTrendDbContext context)
    {
        var existingNames = await context.ApiDataSources
            .Select(s => s.Name)
            .ToListAsync();

        var added = false;
        foreach (var (name, baseUrl) in DefaultSources)
        {
            if (existingNames.Contains(name))
            {
                continue;
            }

            await context.ApiDataSources.AddAsync(new ApiDataSource
            {
                Name = name,
                BaseUrl = baseUrl,
                IsActive = true
            });
            added = true;
        }

        if (added)
        {
            await context.SaveChangesAsync();
        }
    }
}
