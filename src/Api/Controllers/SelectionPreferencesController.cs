using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Dtos;
using Shared.Requests;
using Shared.Responses;

namespace Api.Controllers;

/// <summary>
/// Controller for managing selection preferences and recalculating originals.
/// </summary>
[ApiController]
[Route("api/selection-preferences")]
[Produces("application/json")]
public class SelectionPreferencesController : ControllerBase
{
    private readonly IOriginalSelectionService _service;
    private readonly ILogger<SelectionPreferencesController> _logger;

    public SelectionPreferencesController(IOriginalSelectionService service, ILogger<SelectionPreferencesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Get the current selection configuration.
    /// </summary>
    [HttpGet("config")]
    [ProducesResponseType(typeof(SelectionConfigDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SelectionConfigDto>> GetConfig(CancellationToken ct = default)
    {
        var config = await _service.GetConfigAsync(ct);
        return Ok(config);
    }

    /// <summary>
    /// Get all path priority preferences.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SelectionPreferenceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SelectionPreferenceDto>>> GetPreferences(CancellationToken ct = default)
    {
        var preferences = await _service.GetPreferencesAsync(ct);
        return Ok(preferences);
    }

    /// <summary>
    /// Save path priority preferences.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SavePreferences([FromBody] SavePreferencesRequest request, CancellationToken ct = default)
    {
        await _service.SavePreferencesAsync(request, ct);
        return Ok();
    }

    /// <summary>
    /// Reset preferences to default values.
    /// </summary>
    [HttpPost("reset")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetToDefaults(CancellationToken ct = default)
    {
        await _service.ResetToDefaultsAsync(ct);
        return Ok();
    }

    /// <summary>
    /// Recalculate original file selections for duplicate groups.
    /// </summary>
    [HttpPost("recalculate")]
    [ProducesResponseType(typeof(RecalculateOriginalsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecalculateOriginalsResponse>> RecalculateOriginals(
        [FromBody] RecalculateOriginalsRequest? request,
        CancellationToken ct = default)
    {
        request ??= new RecalculateOriginalsRequest();
        var result = await _service.RecalculateOriginalsAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Calculate score for a specific file.
    /// </summary>
    [HttpGet("score/{fileId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFileScore(Guid fileId, CancellationToken ct = default)
    {
        var score = await _service.CalculateFileScoreAsync(fileId, ct);
        return Ok(new { fileId, score });
    }
}
