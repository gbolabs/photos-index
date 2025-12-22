using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Dtos;
using Shared.Requests;
using Shared.Responses;

namespace Api.Controllers;

/// <summary>
/// Controller for managing indexing status and progress.
/// </summary>
[ApiController]
[Route("api/indexing")]
[Produces("application/json")]
public class IndexingStatusController : ControllerBase
{
    private readonly IIndexingStatusService _service;
    private readonly ILogger<IndexingStatusController> _logger;

    public IndexingStatusController(IIndexingStatusService service, ILogger<IndexingStatusController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Get the current indexing status.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(IndexingStatusDto), StatusCodes.Status200OK)]
    public ActionResult<IndexingStatusDto> GetStatus()
    {
        var status = _service.GetStatus();
        return Ok(status);
    }

    /// <summary>
    /// Start indexing for a directory.
    /// </summary>
    [HttpPost("start")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public ActionResult StartIndexing([FromBody] StartIndexingRequest request)
    {
        _logger.LogInformation(
            "Starting indexing for directory {DirectoryId} at path {DirectoryPath}",
            request.DirectoryId,
            request.DirectoryPath);

        _service.StartIndexing(request.DirectoryId, request.DirectoryPath);

        return Accepted();
    }

    /// <summary>
    /// Update indexing progress.
    /// </summary>
    [HttpPost("progress")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public ActionResult UpdateProgress([FromBody] UpdateProgressRequest request)
    {
        _logger.LogDebug(
            "Updating progress: Scanned={Scanned}, Ingested={Ingested}, Failed={Failed}",
            request.FilesScanned,
            request.FilesIngested,
            request.FilesFailed);

        _service.UpdateProgress(request.FilesScanned, request.FilesIngested, request.FilesFailed);

        return NoContent();
    }

    /// <summary>
    /// Stop the current indexing operation.
    /// </summary>
    [HttpPost("stop")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult StopIndexing()
    {
        _logger.LogInformation("Stopping indexing operation");

        _service.StopIndexing();

        return NoContent();
    }
}
