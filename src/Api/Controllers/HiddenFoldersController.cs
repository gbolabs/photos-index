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

    // Size rule endpoints

    /// <summary>
    /// Get all size-based hiding rules.
    /// </summary>
    [HttpGet("size-rules")]
    [ProducesResponseType(typeof(IReadOnlyList<HiddenSizeRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<HiddenSizeRuleDto>>> GetSizeRules(CancellationToken ct = default)
    {
        var result = await _service.GetAllSizeRulesAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// Preview files that would be hidden by a size rule.
    /// </summary>
    [HttpGet("size-rules/preview")]
    [ProducesResponseType(typeof(SizeRulePreviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SizeRulePreviewDto>> PreviewSizeRule(
        [FromQuery] int maxWidth = 256,
        [FromQuery] int maxHeight = 256,
        CancellationToken ct = default)
    {
        var result = await _service.PreviewSizeRuleAsync(maxWidth, maxHeight, ct);
        return Ok(result);
    }

    /// <summary>
    /// Create a size rule and hide matching files.
    /// </summary>
    [HttpPost("size-rules")]
    [ProducesResponseType(typeof(HiddenSizeRuleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HiddenSizeRuleDto>> CreateSizeRule(
        [FromBody] CreateHiddenSizeRuleRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _service.CreateSizeRuleAsync(request, ct);
            return CreatedAtAction(nameof(GetSizeRules), null, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiErrorResponse.Conflict(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating size rule");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiErrorResponse.InternalError("An error occurred while creating the size rule"));
        }
    }

    /// <summary>
    /// Delete a size rule and unhide affected files.
    /// </summary>
    [HttpDelete("size-rules/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSizeRule(Guid id, CancellationToken ct = default)
    {
        var deleted = await _service.DeleteSizeRuleAsync(id, ct);

        if (!deleted)
            return NotFound(ApiErrorResponse.NotFound($"Size rule with ID {id} not found"));

        return NoContent();
    }
}
