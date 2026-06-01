using Microsoft.EntityFrameworkCore;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Data.Seeders;

public static class ResearchTopicSeeder
{
    public static async Task<List<ResearchTopic>> SeedAsync(ScholarTrendDbContext context)
    {
        if (await context.ResearchTopics.AnyAsync())
        {
            return await context.ResearchTopics.OrderBy(t => t.Id).ToListAsync();
        }

        var topics = new List<ResearchTopic>
        {
            new() { TopicName = "Artificial Intelligence", Description = "Foundational AI methods, agents, and intelligent systems." },
            new() { TopicName = "Data Science", Description = "Data-driven discovery, analytics, and modeling." },
            new() { TopicName = "Software Engineering", Description = "Software design, testing, architecture, and quality." },
            new() { TopicName = "Cyber Security", Description = "Threat detection, privacy, and resilient digital systems." },
            new() { TopicName = "Cloud Computing", Description = "Distributed infrastructure, orchestration, and scalable services." }
        };

        await context.ResearchTopics.AddRangeAsync(topics);
        await context.SaveChangesAsync();
        return topics;
    }
}
