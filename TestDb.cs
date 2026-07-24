using System;
using System.Linq;
using ScholarTrend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ScholarTrend.Domain.Entities;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ScholarTrendDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=ScholarTrendDb;Username=postgres;Password=123");
        using var context = new ScholarTrendDbContext(optionsBuilder.Options);

        var maxYear = await context.KeywordTrends.MaxAsync(t => (int?)t.Year);
        var maxMonth = await context.KeywordTrends.Where(t => t.Year == maxYear).MaxAsync(t => (int?)t.Month);
        Console.WriteLine($"MaxYear: {maxYear}, MaxMonth: {maxMonth}");

        var count = await context.KeywordTrends.CountAsync(t => t.Year == maxYear && t.Month == maxMonth && t.PaperCount > 0);
        Console.WriteLine($"Count (Year={maxYear}, Month={maxMonth}, PaperCount>0): {count}");
    }
}
