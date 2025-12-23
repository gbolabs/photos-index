using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Dtos;
using Shared.Requests;
using Shared.Responses;

namespace Api.Controllers;

/// <summary>
/// Controller for managing indexed files.
/// </summary>
[ApiController]
[Route("api/files")]
[Produces("application/json")]
public class IndexedFilesController : ControllerBase
{
    private readonly IIndexedFileService _service;
    private readonly ILogger<IndexedFilesController> _logger;

    public IndexedFilesController(IIndexedFileService service, ILogger<IndexedFilesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Query indexed files with filtering and pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<IndexedFileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<IndexedFileDto>>> Query(
        [FromQuery] FileQueryParameters query,
        CancellationToken ct = default)
    {
        try
        {
            // Validate query parameters
            if (query.Page < 1)
                return BadRequest(ApiErrorResponse.BadRequest("Page must be at least 1"));
            
            if (query.PageSize < 1 || query.PageSize > 1000)
                return BadRequest(ApiErrorResponse.BadRequest("PageSize must be between 1 and 1000"));

            var result = await _service.QueryAsync(query, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying indexed files");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                ApiErrorResponse.InternalError("An error occurred while querying files"));
        }
    }

    /// <summary>
    /// Get a file by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(IndexedFileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IndexedFileDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _service.GetByIdAsync(id, ct);

        if (result is null)
            return NotFound(ApiErrorResponse.NotFound($"File with ID {id} not found"));

        return Ok(result);
    }

    /// <summary>
    /// Get the thumbnail for a file.
    /// </summary>
    [HttpGet("{id:guid}/thumbnail")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetThumbnail(Guid id, CancellationToken ct = default)
    {
        var thumbnail = await _service.GetThumbnailAsync(id, ct);

        if (thumbnail is null)
            return NotFound(ApiErrorResponse.NotFound($"Thumbnail for file {id} not found"));

        return File(thumbnail, "image/jpeg");
    }

    /// <summary>
    /// Download the original file.
    /// </summary>
    [HttpGet("{id:guid}/download")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct = default)
    {
        var result = await _service.DownloadFileAsync(id, ct);

        if (result is null)
            return NotFound(ApiErrorResponse.NotFound($"File with ID {id} not found"));

        var (content, fileName, contentType) = result.Value;
        return File(content, contentType, fileName);
    }

    /// <summary>
    /// Batch ingest files from the indexing service.
    /// </summary>
    [HttpPost("batch")]
    [ProducesResponseType(typeof(BatchOperationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BatchOperationResponse>> BatchIngest(
        [FromBody] BatchIngestFilesRequest request,
        CancellationToken ct = default)
    {
        if (request.Files.Count == 0)
        {
            return BadRequest(ApiErrorResponse.BadRequest("At least one file is required"));
        }

        var result = await _service.BatchIngestAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get file statistics.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(FileStatisticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FileStatisticsDto>> GetStatistics(CancellationToken ct = default)
    {
        var result = await _service.GetStatisticsAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// Get metadata for multiple files in a single request.
    /// </summary>
    [HttpPost("metadata/batch")]
    [ProducesResponseType(typeof(IReadOnlyList<IndexedFileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<IndexedFileDto>>> GetBatchMetadata(
        [FromBody] IReadOnlyList<Guid> fileIds,
        CancellationToken ct = default)
    {
        try
        {
            if (fileIds == null || fileIds.Count == 0)
            {
                return BadRequest(ApiErrorResponse.BadRequest("At least one file ID is required"));
            }

            if (fileIds.Count > 100)
            {
                return BadRequest(ApiErrorResponse.BadRequest("Maximum of 100 file IDs allowed per request"));
            }

            var result = await _service.GetBatchMetadataAsync(fileIds, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting batch metadata for files");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiErrorResponse.InternalError("An error occurred while getting file metadata"));
        }
    }

    /// <summary>
    /// Soft delete a file.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var deleted = await _service.SoftDeleteAsync(id, ct);

        if (!deleted)
            return NotFound(ApiErrorResponse.NotFound($"File with ID {id} not found"));

        return NoContent();
    }

    /// <summary>
    /// Check which files need to be reindexed based on modification time.
    /// Returns only files that have been modified since last indexed.
    /// </summary>
    [HttpPost("needs-reindex")]
    [ProducesResponseType(typeof(IReadOnlyList<FileNeedsReindexDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<FileNeedsReindexDto>>> CheckNeedsReindex(
        [FromBody] CheckFilesNeedReindexRequest request,
        CancellationToken ct = default)
    {
        if (request.Files.Count == 0)
        {
            return BadRequest(ApiErrorResponse.BadRequest("At least one file is required"));
        }

        var result = await _service.CheckNeedsReindexAsync(request, ct);
        return Ok(result);
    }
}
