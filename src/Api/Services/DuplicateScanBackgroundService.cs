using System.Collections.Concurrent;
using System.Threading.Channels;
using Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Api.Hubs;
using Shared.Dtos;

namespace Api.Services;

/// <summary>
/// Background service that processes duplicate scan requests asynchronously.
/// </summary>
public class DuplicateScanBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<IndexerHub> _hubContext;
    private readonly ILogger<DuplicateScanBackgroundService> _logger;
    private readonly Channel<DuplicateScanJob> _jobChannel;

    // Track active jobs
    private static readonly ConcurrentDictionary<string, DuplicateScanJob> _jobs = new();

    public DuplicateScanBackgroundService(
        IServiceScopeFactory scopeFactory,
        IHubContext<IndexerHub> hubContext,
        ILogger<DuplicateScanBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
        _jobChannel = Channel.CreateUnbounded<DuplicateScanJob>();
    }

    /// <summary>
    /// Queue a new duplicate scan job.
    /// </summary>
    public string QueueScanJob()
    {
        var job = new DuplicateScanJob
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Status = "queued",
            QueuedAt = DateTime.UtcNow
        };

        _jobs[job.Id] = job;
        _jobChannel.Writer.TryWrite(job);

        _logger.LogInformation("Queued duplicate scan job {JobId}", job.Id);
        return job.Id;
    }

    /// <summary>
    /// Get the status of a scan job.
    /// </summary>
    public DuplicateScanJob? GetJobStatus(string jobId)
    {
        return _jobs.TryGetValue(jobId, out var job) ? job : null;
    }

    /// <summary>
    /// Get all recent jobs.
    /// </summary>
    public IEnumerable<DuplicateScanJob> GetRecentJobs()
    {
        return _jobs.Values.OrderByDescending(j => j.QueuedAt).Take(10);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Duplicate scan background service started");

        await foreach (var job in _jobChannel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessJobAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing duplicate scan job {JobId}", job.Id);
                job.Status = "failed";
                job.ErrorMessage = ex.Message;
                job.CompletedAt = DateTime.UtcNow;
                await BroadcastJobUpdate(job);
            }
        }
    }

    private async Task ProcessJobAsync(DuplicateScanJob job, CancellationToken ct)
    {
        job.Status = "running";
        job.StartedAt = DateTime.UtcNow;
        await BroadcastJobUpdate(job);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PhotosDbContext>();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Get total file count for progress
        job.TotalFiles = await dbContext.IndexedFiles.CountAsync(ct);
        await BroadcastJobUpdate(job);

        _logger.LogInformation("Starting duplicate scan for {TotalFiles} files", job.TotalFiles);

        // Find all hashes that appear more than once
        var duplicateHashes = await dbContext.IndexedFiles
            .Where(f => f.FileHash != null && f.FileHash != "")
            .GroupBy(f => f.FileHash)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToListAsync(ct);

        job.TotalDuplicateHashes = duplicateHashes.Count;
        await BroadcastJobUpdate(job);

        _logger.LogInformation("Found {Count} hashes with duplicates", duplicateHashes.Count);

        // Get existing groups
        var existingGroups = await dbContext.DuplicateGroups
            .ToDictionaryAsync(g => g.Hash, g => g, ct);

        var processed = 0;
        foreach (var hash in duplicateHashes)
        {
            if (string.IsNullOrEmpty(hash))
                continue;

            var files = await dbContext.IndexedFiles
                .Where(f => f.FileHash == hash)
                .ToListAsync(ct);

            if (existingGroups.TryGetValue(hash, out var existingGroup))
            {
                existingGroup.FileCount = files.Count;
                existingGroup.TotalSize = files.Sum(f => f.FileSize);

                foreach (var file in files.Where(f => f.DuplicateGroupId != existingGroup.Id))
                {
                    file.DuplicateGroupId = existingGroup.Id;
                    file.IsDuplicate = true;
                }

                if (!files.Any(f => !f.IsDuplicate))
                {
                    files.First().IsDuplicate = false;
                }

                job.GroupsUpdated++;
            }
            else
            {
                var group = new Database.Entities.DuplicateGroup
                {
                    Id = Guid.NewGuid(),
                    Hash = hash,
                    FileCount = files.Count,
                    TotalSize = files.Sum(f => f.FileSize),
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                };
                dbContext.DuplicateGroups.Add(group);

                foreach (var file in files)
                {
                    file.DuplicateGroupId = group.Id;
                    file.IsDuplicate = true;
                }

                files.OrderBy(f => f.IndexedAt).First().IsDuplicate = false;
                job.NewGroupsCreated++;
            }

            processed++;

            // Update progress every 100 hashes
            if (processed % 100 == 0)
            {
                job.ProcessedHashes = processed;
                await BroadcastJobUpdate(job);
            }
        }

        await dbContext.SaveChangesAsync(ct);

        stopwatch.Stop();

        // Calculate final stats
        job.TotalGroups = await dbContext.DuplicateGroups.CountAsync(ct);
        job.TotalDuplicateFiles = await dbContext.IndexedFiles
            .CountAsync(f => f.DuplicateGroupId != null, ct);
        job.PotentialSavingsBytes = await dbContext.DuplicateGroups
            .Where(g => g.FileCount > 0)
            .Select(g => g.TotalSize - (g.TotalSize / g.FileCount))
            .SumAsync(ct);

        job.Status = "completed";
        job.CompletedAt = DateTime.UtcNow;
        job.DurationMs = stopwatch.ElapsedMilliseconds;
        job.ProcessedHashes = processed;

        await BroadcastJobUpdate(job);

        _logger.LogInformation(
            "Duplicate scan completed: {NewGroups} new, {Updated} updated, {Total} total groups in {Duration}ms",
            job.NewGroupsCreated, job.GroupsUpdated, job.TotalGroups, job.DurationMs);
    }

    private async Task BroadcastJobUpdate(DuplicateScanJob job)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("DuplicateScanProgress", job);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast job update");
        }
    }
}

/// <summary>
/// Represents a duplicate scan job.
/// </summary>
public class DuplicateScanJob
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "queued"; // queued, running, completed, failed
    public DateTime QueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long DurationMs { get; set; }
    public string? ErrorMessage { get; set; }

    // Progress
    public int TotalFiles { get; set; }
    public int TotalDuplicateHashes { get; set; }
    public int ProcessedHashes { get; set; }

    // Results
    public int NewGroupsCreated { get; set; }
    public int GroupsUpdated { get; set; }
    public int TotalGroups { get; set; }
    public int TotalDuplicateFiles { get; set; }
    public long PotentialSavingsBytes { get; set; }
}
