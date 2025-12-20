using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Dtos;
using Shared.Requests;
using Shared.Responses;

namespace Api.Controllers;

/// <summary>
/// Controller for managing scan directories.
/// </summary>
[ApiController]
[Route("api/scan-directories")]
[Produces("application/json")]
public class ScanDirectoriesController : ControllerBase
{
    private readonly IScanDirectoryService _service;
    private readonly ILogger<ScanDirectoriesController> _logger;

    public ScanDirectoriesController(IScanDirectoryService service, ILogger<ScanDirectoriesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Get all scan directories with pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ScanDirectoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ScanDirectoryDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        var result = await _service.GetAllAsync(page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a scan directory by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ScanDirectoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScanDirectoryDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _service.GetByIdAsync(id, ct);

        if (result is null)
            return NotFound(ApiErrorResponse.NotFound($"Scan directory with ID {id} not found"));

        return Ok(result);
    }

    /// <summary>
    /// Create a new scan directory.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ScanDirectoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ScanDirectoryDto>> Create(
        [FromBody] CreateScanDirectoryRequest request,
        CancellationToken ct = default)
    {
        // Check for duplicate path
        if (await _service.PathExistsAsync(request.Path, ct))
        {
            return Conflict(ApiErrorResponse.Conflict($"A scan directory with path '{request.Path}' already exists"));
        }

        var result = await _service.CreateAsync(request, ct);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            result);
    }

    /// <summary>
    /// Update an existing scan directory.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ScanDirectoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ScanDirectoryDto>> Update(
        Guid id,
        [FromBody] UpdateScanDirectoryRequest request,
        CancellationToken ct = default)
    {
        // Check for duplicate path if path is being changed
        if (request.Path is not null)
        {
            var existing = await _service.GetByIdAsync(id, ct);
            if (existing is not null && existing.Path != request.Path && await _service.PathExistsAsync(request.Path, ct))
            {
                return Conflict(ApiErrorResponse.Conflict($"A scan directory with path '{request.Path}' already exists"));
            }
        }

        var result = await _service.UpdateAsync(id, request, ct);

        if (result is null)
            return NotFound(ApiErrorResponse.NotFound($"Scan directory with ID {id} not found"));

        return Ok(result);
    }

    /// <summary>
    /// Delete a scan directory.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var deleted = await _service.DeleteAsync(id, ct);

        if (!deleted)
            return NotFound(ApiErrorResponse.NotFound($"Scan directory with ID {id} not found"));

        return NoContent();
    }

    /// <summary>
    /// Trigger a scan for a directory.
    /// </summary>
    [HttpPost("{id:guid}/trigger-scan")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TriggerScan(Guid id, CancellationToken ct = default)
    {
        var triggered = await _service.TriggerScanAsync(id, ct);

        if (!triggered)
            return NotFound(ApiErrorResponse.NotFound($"Scan directory with ID {id} not found"));

        return Accepted();
    }
}
