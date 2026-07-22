using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Data.Seeders;

public static class TrendSeeder
{
    /// <summary>
    /// Trend tables are populated by <see cref="Services.TrendAggregationService"/> via Hangfire.
    /// Seeding fixed-window rows here caused stale data and blocked EnsureBuilt.
    /// </summary>
    public static Task SeedAsync(ScholarTrendDbContext context) => Task.CompletedTask;
}
