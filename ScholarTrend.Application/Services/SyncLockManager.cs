using System.Collections.Concurrent;

namespace ScholarTrend.Application.Services;

public static class SyncLockManager
{
    private static readonly ConcurrentDictionary<string, SyncLockInfo> _locks = new();
    private static readonly TimeSpan DefaultLockTimeout = TimeSpan.FromMinutes(10);

    public static bool TryAcquireLock(string sourceName, string syncType, string triggerBy, out SyncLockInfo? lockInfo)
    {
        var lockKey = sourceName.ToLowerInvariant();
        var now = DateTime.UtcNow;

        lockInfo = new SyncLockInfo
        {
            LockKey = lockKey,
            SyncType = syncType,
            TriggeredBy = triggerBy,
            AcquiredAt = now,
            ExpiresAt = now.Add(DefaultLockTimeout)
        };

        var existingLock = _locks.GetOrAdd(lockKey, lockInfo);

        if (existingLock != lockInfo && existingLock.ExpiresAt > now)
        {
            return false;
        }

        _locks[lockKey] = lockInfo;
        return true;
    }

    public static void ReleaseLock(string sourceName)
    {
        var lockKey = sourceName.ToLowerInvariant();
        _locks.TryRemove(lockKey, out _);
    }

    public static SyncLockInfo? GetLockStatus(string sourceName)
    {
        var lockKey = sourceName.ToLowerInvariant();
        _locks.TryGetValue(lockKey, out var lockInfo);

        if (lockInfo != null && lockInfo.ExpiresAt < DateTime.UtcNow)
        {
            _locks.TryRemove(lockKey, out _);
            return null;
        }

        return lockInfo;
    }

    public static Dictionary<string, SyncLockInfo> GetAllLockStatuses()
    {
        var now = DateTime.UtcNow;
        var statuses = new Dictionary<string, SyncLockInfo>();

        foreach (var kvp in _locks)
        {
            if (kvp.Value.ExpiresAt > now)
            {
                statuses[kvp.Key] = kvp.Value;
            }
            else
            {
                _locks.TryRemove(kvp.Key, out _);
            }
        }

        return statuses;
    }

    public static bool IsAnySyncRunning()
    {
        var now = DateTime.UtcNow;
        return _locks.Values.Any(l => l.ExpiresAt > now);
    }
}

public class SyncLockInfo
{
    public string LockKey { get; set; } = string.Empty;
    public string SyncType { get; set; } = string.Empty;
    public string TriggeredBy { get; set; } = string.Empty;
    public DateTime AcquiredAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
