using Microsoft.EntityFrameworkCore;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Data.Seeders;

public static class AuthorSeeder
{
    public static async Task<List<Author>> SeedAsync(ScholarTrendDbContext context)
    {
        if (await context.Authors.AnyAsync())
        {
            return await context.Authors.OrderBy(a => a.Id).ToListAsync();
        }

        var authors = new List<Author>
        {
            new() { Name = "Dr. An Nguyen", Affiliation = "National University of Ho Chi Minh City", ExternalId = "AUTH-0001", Country = "Vietnam", HIndex = 34, TotalCitations = 4200 },
            new() { Name = "Dr. Linh Tran", Affiliation = "Hanoi University of Science and Technology", ExternalId = "AUTH-0002", Country = "Vietnam", HIndex = 29, TotalCitations = 3100 },
            new() { Name = "Dr. Minh Le", Affiliation = "Singapore University of Technology and Design", ExternalId = "AUTH-0003", Country = "Singapore", HIndex = 41, TotalCitations = 5600 },
            new() { Name = "Dr. Sarah Johnson", Affiliation = "University of California, Berkeley", ExternalId = "AUTH-0004", Country = "USA", HIndex = 58, TotalCitations = 12400 },
            new() { Name = "Dr. David Chen", Affiliation = "Tsinghua University", ExternalId = "AUTH-0005", Country = "China", HIndex = 47, TotalCitations = 8800 },
            new() { Name = "Dr. Priya Patel", Affiliation = "Indian Institute of Technology Bombay", ExternalId = "AUTH-0006", Country = "India", HIndex = 39, TotalCitations = 6900 },
            new() { Name = "Dr. Omar Hassan", Affiliation = "King Abdullah University of Science and Technology", ExternalId = "AUTH-0007", Country = "Saudi Arabia", HIndex = 31, TotalCitations = 4000 },
            new() { Name = "Dr. Elena Rossi", Affiliation = "Politecnico di Milano", ExternalId = "AUTH-0008", Country = "Italy", HIndex = 28, TotalCitations = 2700 },
            new() { Name = "Dr. James Wilson", Affiliation = "University of Oxford", ExternalId = "AUTH-0009", Country = "UK", HIndex = 44, TotalCitations = 7600 },
            new() { Name = "Dr. Aisha Rahman", Affiliation = "University of Melbourne", ExternalId = "AUTH-0010", Country = "Australia", HIndex = 26, TotalCitations = 2200 }
        };

        await context.Authors.AddRangeAsync(authors);
        await context.SaveChangesAsync();
        return authors;
    }
}
