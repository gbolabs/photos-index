using Api.Hubs;
using Api.Services;
using Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReprocessController : ControllerBase
{
    private readonly IReprocessService _reprocessService;

    public ReprocessController(IReprocessService reprocessService)
    {
        _reprocessService = reprocessService;
    }

    /// <summary>
    /// Reprocess a single file by ID
    /// </summary>
    [HttpPost("file/{fileId:guid}")]
    public async Task<ActionResult<ReprocessResult>> ReprocessFile(Guid fileId, CancellationToken ct)
    {
        var result = await _reprocessService.ReprocessFileAsync(fileId, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Reprocess multiple files by ID
    /// </summary>
    [HttpPost("files")]
    public async Task<ActionResult<ReprocessResult>> ReprocessFiles(
        [FromBody] ReprocessFilesRequest request,
        CancellationToken ct)
    {
        var result = await _reprocessService.ReprocessFilesAsync(request.FileIds, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Reprocess files by filter (MissingMetadata, MissingThumbnail, Failed, Heic)
    /// </summary>
    [HttpPost("filter/{filter}")]
    public async Task<ActionResult<ReprocessResult>> ReprocessByFilter(
        ReprocessFilter filter,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        var result = await _reprocessService.ReprocessByFilterAsync(filter, limit, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Get count of files needing reprocessing by filter
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<ReprocessStats>> GetStats(
        [FromServices] PhotosDbContext db,
        CancellationToken ct)
    {
        var stats = new ReprocessStats
        {
            MissingMetadata = await db.IndexedFiles.CountAsync(f => f.MetadataProcessedAt == null, ct),
            MissingThumbnail = await db.IndexedFiles.CountAsync(f => f.ThumbnailProcessedAt == null, ct),
            Failed = await db.IndexedFiles.CountAsync(f => f.LastError != null, ct),
            HeicUnprocessed = await db.IndexedFiles.CountAsync(f =>
                f.FileName.ToLower().EndsWith(".heic") && f.MetadataProcessedAt == null, ct),
            ConnectedIndexers = IndexerHub.GetConnectedIndexerCount()
        };
        return Ok(stats);
    }

    /// <summary>
    /// Reprocess all files in a duplicate group
    /// </summary>
    [HttpPost("duplicate-group/{groupId:guid}")]
    public async Task<ActionResult<ReprocessResult>> ReprocessDuplicateGroup(Guid groupId, CancellationToken ct)
    {
        var result = await _reprocessService.ReprocessDuplicateGroupAsync(groupId, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Reprocess files in multiple duplicate groups
    /// </summary>
    [HttpPost("duplicate-groups")]
    public async Task<ActionResult<ReprocessResult>> ReprocessDuplicateGroups(
        [FromBody] ReprocessDuplicateGroupsRequest request,
        CancellationToken ct)
    {
        var result = await _reprocessService.ReprocessDuplicateGroupsAsync(request.GroupIds, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Reprocess all files in a directory
    /// </summary>
    [HttpPost("directory/{directoryId:guid}")]
    public async Task<ActionResult<ReprocessResult>> ReprocessDirectory(
        Guid directoryId,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        var result = await _reprocessService.ReprocessDirectoryAsync(directoryId, limit, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public record ReprocessDuplicateGroupsRequest(IEnumerable<Guid> GroupIds);

public record ReprocessFilesRequest(IEnumerable<Guid> FileIds);

public record ReprocessStats
{
    public int MissingMetadata { get; init; }
    public int MissingThumbnail { get; init; }
    public int Failed { get; init; }
    public int HeicUnprocessed { get; init; }
    public int ConnectedIndexers { get; init; }
}
