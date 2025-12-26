using Api.Hubs;
using Database;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public interface IReprocessService
{
    Task<ReprocessResult> ReprocessFileAsync(Guid fileId, CancellationToken ct);
    Task<ReprocessResult> ReprocessFilesAsync(IEnumerable<Guid> fileIds, CancellationToken ct);
    Task<ReprocessResult> ReprocessByFilterAsync(ReprocessFilter filter, int? limit, CancellationToken ct);
}

public class ReprocessService : IReprocessService
{
    private readonly IHubContext<IndexerHub> _hubContext;
    private readonly PhotosDbContext _dbContext;
    private readonly ILogger<ReprocessService> _logger;

    public ReprocessService(
        IHubContext<IndexerHub> hubContext,
        PhotosDbContext dbContext,
        ILogger<ReprocessService> logger)
    {
        _hubContext = hubContext;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ReprocessResult> ReprocessFileAsync(Guid fileId, CancellationToken ct)
    {
        var file = await _dbContext.IndexedFiles.FindAsync([fileId], ct);
        if (file is null)
            return ReprocessResult.NotFound(fileId);

        if (!IndexerHub.GetConnectedIndexers().Any())
            return ReprocessResult.NoIndexerConnected();

        // Reset processing state
        file.MetadataProcessedAt = null;
        file.ThumbnailProcessedAt = null;
        file.LastError = null;
        await _dbContext.SaveChangesAsync(ct);

        await _hubContext.Clients.All.SendAsync("ReprocessFile", fileId, file.FilePath, ct);
        _logger.LogInformation("Sent reprocess command for file {FileId}: {Path}", fileId, file.FilePath);

        return ReprocessResult.Queued(1);
    }

    public async Task<ReprocessResult> ReprocessFilesAsync(IEnumerable<Guid> fileIds, CancellationToken ct)
    {
        var fileIdList = fileIds.ToList();
        var files = await _dbContext.IndexedFiles
            .Where(f => fileIdList.Contains(f.Id))
            .ToListAsync(ct);

        if (files.Count == 0)
            return ReprocessResult.NotFound(Guid.Empty);

        if (!IndexerHub.GetConnectedIndexers().Any())
            return ReprocessResult.NoIndexerConnected();

        // Reset processing state
        foreach (var file in files)
        {
            file.MetadataProcessedAt = null;
            file.ThumbnailProcessedAt = null;
            file.LastError = null;
        }
        await _dbContext.SaveChangesAsync(ct);

        var requests = files.Select(f => new ReprocessFileRequest(f.Id, f.FilePath)).ToList();
        await _hubContext.Clients.All.SendAsync("ReprocessFiles", requests, ct);

        _logger.LogInformation("Sent reprocess command for {Count} files", files.Count);

        return ReprocessResult.Queued(files.Count);
    }

    public async Task<ReprocessResult> ReprocessByFilterAsync(ReprocessFilter filter, int? limit, CancellationToken ct)
    {
        var query = _dbContext.IndexedFiles.AsQueryable();

        query = filter switch
        {
            ReprocessFilter.MissingMetadata => query.Where(f => f.MetadataProcessedAt == null),
            ReprocessFilter.MissingThumbnail => query.Where(f => f.ThumbnailProcessedAt == null),
            ReprocessFilter.Failed => query.Where(f => f.LastError != null),
            ReprocessFilter.Heic => query.Where(f =>
                f.FileName.ToLower().EndsWith(".heic") && f.MetadataProcessedAt == null),
            _ => throw new ArgumentException($"Unknown filter: {filter}")
        };

        var files = await query
            .OrderBy(f => f.IndexedAt)
            .Take(limit ?? 1000)
            .ToListAsync(ct);

        if (files.Count == 0)
            return ReprocessResult.Queued(0);

        if (!IndexerHub.GetConnectedIndexers().Any())
            return ReprocessResult.NoIndexerConnected();

        // Reset processing state
        foreach (var file in files)
        {
            file.MetadataProcessedAt = null;
            file.ThumbnailProcessedAt = null;
            file.LastError = null;
        }
        await _dbContext.SaveChangesAsync(ct);

        var requests = files.Select(f => new ReprocessFileRequest(f.Id, f.FilePath)).ToList();
        await _hubContext.Clients.All.SendAsync("ReprocessFiles", requests, ct);

        _logger.LogInformation("Sent reprocess command for {Count} files with filter {Filter}",
            files.Count, filter);

        return ReprocessResult.Queued(files.Count);
    }
}

public enum ReprocessFilter
{
    MissingMetadata,
    MissingThumbnail,
    Failed,
    Heic
}

public record ReprocessResult(bool Success, int QueuedCount, string? Error)
{
    public static ReprocessResult Queued(int count) => new(true, count, null);
    public static ReprocessResult NotFound(Guid fileId) => new(false, 0, $"File not found: {fileId}");
    public static ReprocessResult NoIndexerConnected() => new(false, 0, "No indexer connected");
}
