using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.Sync;
using ScholarTrend.Application.Interfaces;

namespace ScholarTrend.Application.Services;

public class SyncSchedulerService : ISyncSchedulerService
{
    private const string CacheKey = "sync_schedule_config";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly ISyncService _syncService;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SyncSchedulerService> _logger;

    public SyncSchedulerService(
        ISyncService syncService,
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<SyncSchedulerService> logger)
    {
        _syncService = syncService;
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
    }

    public Task<List<string>> GetActiveSearchQueriesAsync()
    {
        return Task.FromResult(GetSearchQueries());
    }

    public Task<SyncScheduleDto> GetScheduleConfigAsync()
    {
        if (_cache.TryGetValue(CacheKey, out SyncScheduleDto? cached) && cached != null)
        {
            return Task.FromResult(cached);
        }

        var enabled = _configuration["SyncSchedule:Enabled"];
        var config = new SyncScheduleDto
        {
            Enabled = string.IsNullOrEmpty(enabled) || bool.Parse(enabled),
            CronExpression = _configuration["SyncSchedule:CronExpression"] ?? "0 2 * * *",
            TimeZone = _configuration["SyncSchedule:TimeZone"] ?? "SE Asia Standard Time",
            SearchQueries = GetSearchQueries(),
            LastSyncAt = GetLastSyncTime(),
            NextSyncAt = CalculateNextSyncTime()
        };

        _cache.Set(CacheKey, config, CacheDuration);
        return Task.FromResult(config);
    }

    public async Task<SyncScheduleDto> UpdateScheduleConfigAsync(SyncScheduleConfigRequest request)
    {
        _configuration["SyncSchedule:Enabled"] = request.Enabled.ToString();
        _configuration["SyncSchedule:CronExpression"] = request.CronExpression;
        _configuration["SyncSchedule:TimeZone"] = request.TimeZone;

        if (request.SearchQueries.Count > 0)
        {
            _configuration["SyncSchedule:SearchQueries"] = string.Join(",", request.SearchQueries);
        }

        _cache.Remove(CacheKey);

        await Task.Run(() => UpdateHangfireJobAsync(request.Enabled, request.CronExpression));

        _logger.LogInformation("Sync schedule updated: Enabled={Enabled}, Cron={Cron}", 
            request.Enabled, request.CronExpression);

        return await GetScheduleConfigAsync();
    }

    public async Task<ManualSyncResultDto> TriggerManualSyncAsync(string adminUserId, ManualSyncRequest? request = null)
    {
        var syncType = "Manual";
        _logger.LogInformation("Manual sync triggered by {UserId} for source: {Source}",
            adminUserId, request?.SourceName ?? "all");

        var result = new ManualSyncResultDto
        {
            SyncType = syncType,
            TriggeredAt = DateTime.UtcNow,
            TriggeredBy = adminUserId,
            SourceName = request?.SourceName
        };

        try
        {
            var searchQuery = request?.SearchQuery;
            int? paperLimit = request?.PaperLimit;

            _logger.LogInformation("Manual sync triggered by {UserId} for source: {Source}, query: '{Query}', limit: {Limit}",
                adminUserId, request?.SourceName ?? "all", searchQuery ?? "default", paperLimit?.ToString() ?? "default");

            var multiResult = await _syncService.RunSyncAsync(
                sourceName: request?.SourceName,
                syncType: syncType,
                triggeredBy: adminUserId,
                searchQueries: !string.IsNullOrWhiteSpace(searchQuery) ? new List<string> { searchQuery } : null,
                paperLimit: paperLimit);

            // "Success" means at least one result completed AND nothing failed.
            // Skipped results (lock held by concurrent run) are not considered failures.
            var hasAnyCompleted = multiResult.Results.Any(r => r.Status == "Completed");
            var hasAnyFailed = multiResult.Results.Any(r => r.Status == "Failed");
            result.Success = hasAnyCompleted && !hasAnyFailed;
            result.PapersFetched = multiResult.TotalFetched;
            result.PapersQueued = multiResult.TotalQueued;

            var proposalIds = multiResult.Results
                .Where(r => r.SyncProposalId.HasValue)
                .Select(r => r.SyncProposalId!.Value)
                .Distinct()
                .ToList();

            if (proposalIds.Count > 0)
            {
                result.ProposalId = proposalIds.First();
            }

            result.SourceResults = multiResult.Results.Select(r => new SourceSyncResultDto
            {
                SourceName = r.Source,
                PapersFetched = r.PapersFetched,
                PapersQueued = r.PapersAdded,
                Status = r.Status,
                ErrorMessage = r.Message
            }).ToList();

            var successCount = result.SourceResults.Count(r => r.Status == "AwaitingApproval" || r.Status == "Completed");
            var failedCount = result.SourceResults.Count(r => r.Status == "Failed");
            var skippedCount = result.SourceResults.Count(r => r.Status == "Skipped");
            var totalSources = result.SourceResults.Count;

            if (skippedCount > 0)
            {
                result.Message = $"{skippedCount}/{totalSources} sources skipped (already running). {result.PapersQueued} papers queued.";
            }
            else if (failedCount > 0)
            {
                result.Message = $"{successCount}/{totalSources} sources synced. {failedCount} failed. {result.PapersQueued} papers queued.";
            }
            else
            {
                result.Message = $"All {totalSources} sources synced successfully. {result.PapersQueued} papers queued for approval.";
            }

            _logger.LogInformation("Manual sync completed: Fetched={Fetched}, Queued={Queued}, Skipped={Skipped}",
                result.PapersFetched, result.PapersQueued, skippedCount);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual sync failed");
            result.Success = false;
            result.Message = $"Sync failed: {ex.Message}";
            return result;
        }
    }

    public Task<List<SyncJobInfoDto>> GetJobHistoryAsync(int limit = 50)
    {
        var jobs = new List<SyncJobInfoDto>();

        try
        {
            var storage = JobStorage.Current;
            using var connection = storage.GetConnection();
            
            var succeededJobs = connection.GetRecurringJobs();
            var succeeded = succeededJobs.Where(j => j.Id == "daily-paper-sync").ToList();

            foreach (var job in succeeded)
            {
                jobs.Add(new SyncJobInfoDto
                {
                    Id = 0,
                    JobId = job.Id,
                    JobName = "daily-paper-sync",
                    Status = "Scheduled",
                    StartedAt = null,
                    CompletedAt = null
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve job history from Hangfire");
        }

        return Task.FromResult(jobs);
    }

    private List<string> GetSearchQueries()
    {
        // 1. Try the strongly-typed "SyncSchedule:SearchQueries" array (e.g. from appsettings.json).
        //    Manual children iteration avoids the Get<T>() ambiguity with IMemoryCache.
        var section = _configuration.GetSection("SyncSchedule:SearchQueries");
        var fromSection = section.Exists()
            ? section.GetChildren()
                .Select(c => c.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .ToList()
            : new List<string>();

        if (fromSection.Count > 0)
        {
            return fromSection;
        }

        // 2. Try the flat "SyncSchedule:SearchQueries" CSV value (set by UpdateScheduleConfigAsync).
        var flat = _configuration["SyncSchedule:SearchQueries"];
        if (!string.IsNullOrEmpty(flat))
        {
            return flat
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(q => q.Trim())
                .Where(q => !string.IsNullOrEmpty(q))
                .ToList();
        }

        // 3. Final fallback — keep callers alive even when nothing is configured.
        _logger.LogWarning("No SyncSchedule:SearchQueries configured; falling back to default single query.");
        return ["artificial intelligence"];
    }

    private DateTime? GetLastSyncTime()
    {
        // Last sync time will be retrieved from SyncLogs table via SyncService
        // This is a placeholder - actual implementation uses SyncLogs
        return null;
    }

    private DateTime? CalculateNextSyncTime()
    {
        try
        {
            var cronExpression = _configuration["SyncSchedule:CronExpression"] ?? "0 2 * * *";
            var timezone = _configuration["SyncSchedule:TimeZone"] ?? "SE Asia Standard Time";
            
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            
            var nextOccurrence = GetNextCronOccurrence(cronExpression, now);
            return nextOccurrence.HasValue ? TimeZoneInfo.ConvertTimeToUtc(nextOccurrence.Value, tz) : null;
        }
        catch
        {
            return DateTime.UtcNow.AddDays(1);
        }
    }

    private static DateTime? GetNextCronOccurrence(string cronExpression, DateTime from)
    {
        try
        {
            var parts = cronExpression.Split(' ');
            if (parts.Length < 5) return from.AddDays(1);

            var minute = ParseCronPart(parts[0], 0, 59);
            var hour = ParseCronPart(parts[1], 0, 23);
            var dayOfMonth = parts[2];
            var month = parts[3];
            var dayOfWeek = parts[4];

            if (minute == null || hour == null) return from.AddDays(1);

            var next = from.Date.AddHours(hour.Value).AddMinutes(minute.Value);
            
            if (next <= from)
            {
                next = next.AddDays(1);
            }

            while (next > from)
            {
                if (MatchesDayOfMonth(next, dayOfMonth) && MatchesMonth(next, month) && MatchesDayOfWeek(next, dayOfWeek))
                {
                    return next;
                }
                next = next.AddDays(1);
            }

            return next;
        }
        catch
        {
            return from.AddDays(1);
        }
    }

    private static int? ParseCronPart(string part, int min, int max)
    {
        if (part == "*") return min;
        if (int.TryParse(part, out var value) && value >= min && value <= max) return value;
        return null;
    }

    private static bool MatchesDayOfMonth(DateTime dt, string part)
    {
        return part == "*" || part == dt.Day.ToString();
    }

    private static bool MatchesMonth(DateTime dt, string part)
    {
        return part == "*" || part == dt.Month.ToString();
    }

    private static bool MatchesDayOfWeek(DateTime dt, string part)
    {
        return part == "*" || int.TryParse(part, out var dow) && (int)dt.DayOfWeek == dow;
    }

    private void UpdateHangfireJobAsync(bool enabled, string cronExpression)
    {
        if (enabled)
        {
            RecurringJob.AddOrUpdate<ISyncJob>("daily-paper-sync", job => job.RunAsync(), cronExpression);
            _logger.LogInformation("Hangfire job scheduled with cron: {Cron}", cronExpression);
        }
        else
        {
            RecurringJob.RemoveIfExists("daily-paper-sync");
            _logger.LogInformation("Hangfire job removed");
        }
    }
}
