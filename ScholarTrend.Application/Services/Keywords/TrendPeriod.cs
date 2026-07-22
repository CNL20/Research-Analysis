namespace ScholarTrend.Application.Services.Keywords;

/// <summary>
/// Trend windows: rolling 12 months for default API filters;
/// full paper history (capped) for rebuild/storage after approve/sync.
/// </summary>
public static class TrendPeriod
{
    /// <summary>Default dashboard/API filter when client sends no dates.</summary>
    public const int RollingMonths = 12;

    /// <summary>Max months stored by rebuild job (20 years) to bound DB size.</summary>
    public const int MaxRebuildMonths = 240;

    /// <summary>
    /// Returns a 12-month window ending at the current UTC month (inclusive).
    /// Example (Jul 2026): Aug 2025 … Jul 2026.
    /// </summary>
    public static TrendWindow GetRollingWindow(DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        var windowEnd = MonthStart(now.Year, now.Month);
        var windowStart = windowEnd.AddMonths(-(RollingMonths - 1));

        return BuildWindow(windowStart, windowEnd);
    }

    /// <summary>
    /// Rebuild window spans every month from the earliest browsable paper
    /// through the current month (capped at <see cref="MaxRebuildMonths"/>).
    /// Approve/sync papers outside the last 12 months still get trend rows.
    /// </summary>
    public static TrendWindow GetRebuildWindow(
        IEnumerable<(int Year, int Month)> paperMonths,
        DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        var windowEnd = MonthStart(now.Year, now.Month);

        var months = paperMonths
            .Where(m => m.Year > 0 && m.Month is >= 1 and <= 12)
            .ToList();

        if (months.Count == 0)
        {
            return GetRollingWindow(utcNow);
        }

        var earliest = months
            .MinBy(m => m.Year * 100 + m.Month);

        var windowStart = MonthStart(earliest.Year, earliest.Month);
        var capStart = windowEnd.AddMonths(-(MaxRebuildMonths - 1));
        if (windowStart < capStart)
        {
            windowStart = capStart;
        }

        if (windowStart > windowEnd)
        {
            windowStart = windowEnd;
        }

        return BuildWindow(windowStart, windowEnd);
    }

    private static TrendWindow BuildWindow(DateTime windowStart, DateTime windowEnd)
    {
        var list = new List<(int Year, int Month)>();
        for (var cursor = windowStart; cursor <= windowEnd; cursor = cursor.AddMonths(1))
        {
            list.Add((cursor.Year, cursor.Month));
        }

        return new TrendWindow(windowStart, windowEnd, list);
    }

    private static DateTime MonthStart(int year, int month)
        => new(year, month, 1, 0, 0, 0, DateTimeKind.Utc);

    public readonly record struct TrendWindow(
        DateTime Start,
        DateTime End,
        IReadOnlyList<(int Year, int Month)> Months);
}
