using Microsoft.Extensions.Caching.Memory;
using ScholarTrend.Application.Interfaces;

namespace ScholarTrend.Application.Services;

public class TrendDashboardCacheInvalidator : ITrendDashboardCacheInvalidator
{
    public const string VersionCacheKey = "trends:dashboard:version";

    private readonly IMemoryCache _cache;

    public TrendDashboardCacheInvalidator(IMemoryCache cache)
    {
        _cache = cache;
    }

    public long GetVersion()
    {
        return _cache.GetOrCreate(VersionCacheKey, entry =>
        {
            entry.Priority = CacheItemPriority.NeverRemove;
            return 0L;
        });
    }

    public void Invalidate()
    {
        var next = GetVersion() + 1;
        _cache.Set(VersionCacheKey, next, new MemoryCacheEntryOptions
        {
            Priority = CacheItemPriority.NeverRemove
        });
    }
}
