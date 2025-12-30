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
    Task<ReprocessResult> ReprocessDuplicateGroupAsync(Guid groupId, CancellationToken ct);
    Task<ReprocessResult> ReprocessDuplicateGroupsAsync(IEnumerable<Guid> groupIds, CancellationToken ct);
    Task<ReprocessResult> ReprocessDirectoryAsync(Guid directoryId, int? limit, CancellationToken ct);
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

        if (IndexerHub.GetConnectedIndexerCount() == 0)
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

        if (IndexerHub.GetConnectedIndexerCount() == 0)
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

        // No limit by default - process all matching files
        // The operation is async (via SignalR), so no blocking concerns
        var filesQuery = query.OrderBy(f => f.IndexedAt);
        var files = limit.HasValue
            ? await filesQuery.Take(limit.Value).ToListAsync(ct)
            : await filesQuery.ToListAsync(ct);

        if (files.Count == 0)
            return ReprocessResult.Queued(0);

        if (IndexerHub.GetConnectedIndexerCount() == 0)
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

    public async Task<ReprocessResult> ReprocessDuplicateGroupAsync(Guid groupId, CancellationToken ct)
    {
        var group = await _dbContext.DuplicateGroups
            .Include(g => g.Files)
            .FirstOrDefaultAsync(g => g.Id == groupId, ct);

        if (group is null)
            return new ReprocessResult(false, 0, $"Duplicate group not found: {groupId}");

        if (group.Files.Count == 0)
            return ReprocessResult.Queued(0);

        if (IndexerHub.GetConnectedIndexerCount() == 0)
            return ReprocessResult.NoIndexerConnected();

        // Reset processing state for all files in the group
        foreach (var file in group.Files)
        {
            file.MetadataProcessedAt = null;
            file.ThumbnailProcessedAt = null;
            file.LastError = null;
        }
        await _dbContext.SaveChangesAsync(ct);

        var requests = group.Files.Select(f => new ReprocessFileRequest(f.Id, f.FilePath)).ToList();
        await _hubContext.Clients.All.SendAsync("ReprocessFiles", requests, ct);

        _logger.LogInformation("Sent reprocess command for {Count} files in duplicate group {GroupId}",
            group.Files.Count, groupId);

        return ReprocessResult.Queued(group.Files.Count);
    }

    public async Task<ReprocessResult> ReprocessDuplicateGroupsAsync(IEnumerable<Guid> groupIds, CancellationToken ct)
    {
        var groupIdList = groupIds.ToList();
        var files = await _dbContext.IndexedFiles
            .Where(f => f.DuplicateGroupId != null && groupIdList.Contains(f.DuplicateGroupId.Value))
            .ToListAsync(ct);

        if (files.Count == 0)
            return ReprocessResult.Queued(0);

        if (IndexerHub.GetConnectedIndexerCount() == 0)
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

        _logger.LogInformation("Sent reprocess command for {Count} files in {GroupCount} duplicate groups",
            files.Count, groupIdList.Count);

        return ReprocessResult.Queued(files.Count);
    }

    public async Task<ReprocessResult> ReprocessDirectoryAsync(Guid directoryId, int? limit, CancellationToken ct)
    {
        var directory = await _dbContext.ScanDirectories.FindAsync([directoryId], ct);
        if (directory is null)
            return new ReprocessResult(false, 0, $"Directory not found: {directoryId}");

        var query = _dbContext.IndexedFiles
            .Where(f => f.FilePath.StartsWith(directory.Path))
            .OrderBy(f => f.IndexedAt);

        var files = limit.HasValue
            ? await query.Take(limit.Value).ToListAsync(ct)
            : await query.ToListAsync(ct);

        if (files.Count == 0)
            return ReprocessResult.Queued(0);

        if (IndexerHub.GetConnectedIndexerCount() == 0)
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

        _logger.LogInformation("Sent reprocess command for {Count} files in directory {DirectoryPath}",
            files.Count, directory.Path);

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
