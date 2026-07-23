namespace ScholarTrend.Application.Services.Reports;

/// <summary>
/// Maps scale + momentum into a short research suggestion label for report breakdown rows.
/// </summary>
public static class ResearchSuggestionRules
{
    public const string WorthConsidering = "WorthConsidering";
    public const string MatureSlowing = "MatureSlowing";
    public const string EmergingThin = "EmergingThin";
    public const string Cooling = "Cooling";
    public const string Neutral = "Neutral";

    public const int MinPapersSolid = 10;
    public const int MinPapersEmerging = 5;
    public const double HighScore = 15.0;

    public static string Evaluate(int paperCount, double? growthRate, double? trendingScore)
    {
        var growth = growthRate ?? 0;
        var score = trendingScore ?? 0;

        if (paperCount < MinPapersEmerging && (growth > 0 || score >= HighScore))
            return EmergingThin;

        if (paperCount >= MinPapersSolid && growth > 0 && score >= HighScore)
            return WorthConsidering;

        if (paperCount >= MinPapersSolid && growth <= 0)
            return MatureSlowing;

        if (growth < 0)
            return Cooling;

        if (paperCount >= MinPapersEmerging && growth > 0)
            return WorthConsidering;

        return Neutral;
    }
}
