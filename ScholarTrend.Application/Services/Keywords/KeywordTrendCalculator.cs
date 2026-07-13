namespace ScholarTrend.Application.Services.Keywords;

public static class KeywordTrendCalculator
{
    public static double CalculateTrendingScore(int paperCount, double growthRate, int citationCount)
    {
        return Math.Round(
            (paperCount * 0.65) + (Math.Max(growthRate, 0) / 10.0) + (citationCount / 120.0),
            2);
    }

    public static double CalculateGrowthRate(int previousCount, int currentCount)
    {
        if (previousCount == 0)
        {
            return 0;
        }

        return Math.Round(((currentCount - previousCount) / (double)previousCount) * 100.0, 2);
    }
}
