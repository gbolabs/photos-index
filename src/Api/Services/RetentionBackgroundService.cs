using Microsoft.Extensions.Options;
using Shared.Dtos;
using Shared.Storage;

namespace Api.Services;

/// <summary>
/// Background service that runs daily to clean up expired archived files.
/// </summary>
public class RetentionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<CleanerOptions> _options;
    private readonly ILogger<RetentionBackgroundService> _logger;

    public RetentionBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<CleanerOptions> options,
        ILogger<RetentionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Retention background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Calculate time until next run (default 3 AM)
                var now = DateTime.Now;
                var nextRun = GetNextRunTime(now);
                var delay = nextRun - now;

                if (delay > TimeSpan.Zero)
                {
                    _logger.LogInformation("Next retention cleanup scheduled for {NextRun}", nextRun);
                    await Task.Delay(delay, stoppingToken);
                }

                await RunRetentionCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running retention cleanup");
                // Wait a bit before retrying on error
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("Retention background service stopped");
    }

    private DateTime GetNextRunTime(DateTime now)
    {
        var runTime = _options.Value.RetentionCheckTime;
        var today = now.Date.Add(runTime);

        // If today's run time has passed, schedule for tomorrow
        if (today <= now)
        {
            return today.AddDays(1);
        }

        return today;
    }

    private async Task RunRetentionCleanupAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting retention cleanup");

        using var scope = _scopeFactory.CreateScope();
        var objectStorage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        var options = _options.Value;

        var categories = new[]
        {
            (Category: DeleteCategory.HashDuplicate, Prefix: "hash_duplicate", Days: options.RetentionDaysHashDuplicate),
            (Category: DeleteCategory.NearDuplicate, Prefix: "near_duplicate", Days: options.RetentionDaysNearDuplicate),
            (Category: DeleteCategory.Manual, Prefix: "manual", Days: options.RetentionDaysManual)
        };

        var totalDeleted = 0;

        foreach (var (category, prefix, retentionDays) in categories)
        {
            try
            {
                var deleted = await CleanupCategoryAsync(objectStorage, prefix, retentionDays, ct);
                totalDeleted += deleted;
                _logger.LogInformation("Cleaned up {Deleted} expired files from {Category}", deleted, prefix);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up category {Category}", prefix);
            }
        }

        _logger.LogInformation("Retention cleanup completed, deleted {TotalDeleted} files", totalDeleted);
    }

    private async Task<int> CleanupCategoryAsync(
        IObjectStorage objectStorage,
        string categoryPrefix,
        int retentionDays,
        CancellationToken ct)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        var deleted = 0;
        var archiveBucket = _options.Value.ArchiveBucket;

        // List objects in the category prefix
        var objects = await objectStorage.ListObjectsAsync(archiveBucket, categoryPrefix);

        foreach (var obj in objects)
        {
            if (ct.IsCancellationRequested)
                break;

            // Parse the date from the path (format: category/YYYY-MM/filename)
            if (TryParseArchiveDate(obj.Key, out var archiveDate))
            {
                if (archiveDate < cutoffDate)
                {
                    try
                    {
                        await objectStorage.DeleteAsync(archiveBucket, obj.Key);
                        deleted++;
                        _logger.LogDebug("Deleted expired archive: {Bucket}/{Key}", archiveBucket, obj.Key);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete archive {Bucket}/{Key}", archiveBucket, obj.Key);
                    }
                }
            }
        }

        return deleted;
    }

    private static bool TryParseArchiveDate(string key, out DateTime date)
    {
        date = default;

        // Expected format: category/YYYY-MM/filename
        var parts = key.Split('/');
        if (parts.Length < 2)
            return false;

        var datePart = parts[1]; // YYYY-MM
        if (datePart.Length >= 7 &&
            int.TryParse(datePart[..4], out var year) &&
            int.TryParse(datePart.Substring(5, 2), out var month))
        {
            date = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            return true;
        }

        return false;
    }
}

/// <summary>
/// Configuration options for the cleaner service.
/// </summary>
public class CleanerOptions
{
    public const string ConfigSection = "Cleaner";

    public string ArchiveBucket { get; set; } = "archive";
    public int RetentionDaysHashDuplicate { get; set; } = 180; // 6 months
    public int RetentionDaysNearDuplicate { get; set; } = 730; // 2 years
    public int RetentionDaysManual { get; set; } = 730; // 2 years
    public TimeSpan RetentionCheckTime { get; set; } = TimeSpan.FromHours(3); // 3 AM
}
