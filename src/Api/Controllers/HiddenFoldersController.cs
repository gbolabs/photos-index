using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Dtos;
using Shared.Responses;

namespace Api.Controllers;

/// <summary>
/// Controller for managing hidden folder rules.
/// </summary>
[ApiController]
[Route("api/hidden-folders")]
[Produces("application/json")]
public class HiddenFoldersController : ControllerBase
{
    private readonly IHiddenFolderService _service;
    private readonly ILogger<HiddenFoldersController> _logger;

    public HiddenFoldersController(IHiddenFolderService service, ILogger<HiddenFoldersController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Get all hidden folder rules.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<HiddenFolderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<HiddenFolderDto>>> GetAll(CancellationToken ct = default)
    {
        var result = await _service.GetAllAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// Get folder paths for autocomplete.
    /// </summary>
    [HttpGet("folder-paths")]
    [ProducesResponseType(typeof(IReadOnlyList<FolderPathDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FolderPathDto>>> GetFolderPaths(
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _service.GetFolderPathsAsync(search, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get count of hidden files.
    /// </summary>
    [HttpGet("hidden-count")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> GetHiddenCount(CancellationToken ct = default)
    {
        var count = await _service.GetHiddenCountAsync(ct);
        return Ok(new { count });
    }

    /// <summary>
    /// Create a new hidden folder rule.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(HiddenFolderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HiddenFolderDto>> Create(
        [FromBody] CreateHiddenFolderRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FolderPath))
        {
            return BadRequest(ApiErrorResponse.BadRequest("FolderPath is required"));
        }

        try
        {
            var result = await _service.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetAll), null, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiErrorResponse.Conflict(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating hidden folder rule");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiErrorResponse.InternalError("An error occurred while creating the hidden folder rule"));
        }
    }

    /// <summary>
    /// Delete a hidden folder rule and unhide affected files.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var deleted = await _service.DeleteAsync(id, ct);

        if (!deleted)
            return NotFound(ApiErrorResponse.NotFound($"Hidden folder rule with ID {id} not found"));

        return NoContent();
    }
}
