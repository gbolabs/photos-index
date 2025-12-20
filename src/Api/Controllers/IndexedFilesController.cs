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
    public async Task<ActionResult<PagedResponse<IndexedFileDto>>> Query(
        [FromQuery] FileQueryParameters query,
        CancellationToken ct = default)
    {
        var result = await _service.QueryAsync(query, ct);
        return Ok(result);
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
}
