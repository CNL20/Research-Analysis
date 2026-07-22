namespace ScholarTrend.Application.Interfaces;

/// <summary>
/// Bumps dashboard cache version so /trends/dashboard reflects fresh DB after rebuild.
/// </summary>
public interface ITrendDashboardCacheInvalidator
{
    long GetVersion();

    void Invalidate();
}
