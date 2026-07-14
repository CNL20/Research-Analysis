using Microsoft.EntityFrameworkCore;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Data.Seeders;

public static class SubscriptionPlanSeeder
{
    public static async Task SeedAsync(ScholarTrendDbContext context)
    {
        var existingPlans = await context.SubscriptionPlans.ToListAsync();
        if (existingPlans.Any())
        {
            var needsUpdate = false;
            foreach (var plan in existingPlans)
            {
                if (plan.TargetRole == "PremiumUser")
                {
                    plan.TargetRole = "Researcher";
                    needsUpdate = true;
                }

                if (plan.Name == "Gói Premium (1 Tháng)")
                {
                    plan.Name = "Premium Plan (1 Month)";
                    plan.Description = "1-month premium plan. Unlock all trend analysis features.";
                    needsUpdate = true;
                }
                else if (plan.Name == "Gói Premium (1 Năm)")
                {
                    plan.Name = "Premium Plan (1 Year)";
                    plan.Description = "1-year premium plan (10% discount). Unlock all features.";
                    needsUpdate = true;
                }
            }
            if (needsUpdate)
            {
                await context.SaveChangesAsync();
            }
            return;
        }

        var plans = new List<SubscriptionPlan>
        {
            new SubscriptionPlan
            {
                Name = "Premium Plan (1 Month)",
                Code = "PREM_1M",
                TargetRole = "Researcher",
                PriceVND = 30000,
                PriceUSD = 1.25m, // Relative price
                DurationDays = 30,
                TrialDays = 0,
                IsActive = true,
                Description = "1-month premium plan. Unlock all trend analysis features.",
                CreatedAt = DateTime.UtcNow
            },
            new SubscriptionPlan
            {
                Name = "Premium Plan (1 Year)",
                Code = "PREM_1Y",
                TargetRole = "Researcher",
                PriceVND = 324000, // 30k * 12 months - 10% discount = 324k
                PriceUSD = 13.5m,
                DurationDays = 365,
                TrialDays = 0,
                IsActive = true,
                Description = "1-year premium plan (10% discount). Unlock all features.",
                CreatedAt = DateTime.UtcNow
            }
        };

        await context.SubscriptionPlans.AddRangeAsync(plans);
        await context.SaveChangesAsync();
    }
}
