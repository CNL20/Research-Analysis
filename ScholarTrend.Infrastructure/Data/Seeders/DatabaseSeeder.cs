using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Data.Seeders;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ScholarTrendDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        var hasAnyData = await context.Users.AnyAsync()
            || await context.Journals.AnyAsync()
            || await context.Authors.AnyAsync()
            || await context.Keywords.AnyAsync()
            || await context.ResearchTopics.AnyAsync()
            || await context.ResearchPapers.AnyAsync();

        if (hasAnyData)
        {
            return;
        }

        await RoleSeeder.SeedAsync(roleManager);
        await UserSeeder.SeedAsync(userManager);

        var journals = await JournalSeeder.SeedAsync(context);
        var authors = await AuthorSeeder.SeedAsync(context);
        var keywords = await KeywordSeeder.SeedAsync(context);
        var topics = await ResearchTopicSeeder.SeedAsync(context);
        await ResearchPaperSeeder.SeedAsync(context, journals, authors, keywords, topics);
        await TrendSeeder.SeedAsync(context);
    }
}
