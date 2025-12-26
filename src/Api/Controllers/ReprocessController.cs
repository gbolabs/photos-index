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
            ConnectedIndexers = IndexerHub.GetConnectedIndexers().Count
        };
        return Ok(stats);
    }
}

public record ReprocessFilesRequest(IEnumerable<Guid> FileIds);

public record ReprocessStats
{
    public int MissingMetadata { get; init; }
    public int MissingThumbnail { get; init; }
    public int Failed { get; init; }
    public int HeicUnprocessed { get; init; }
    public int ConnectedIndexers { get; init; }
}
