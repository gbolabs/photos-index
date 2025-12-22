using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Dtos;
using Shared.Requests;
using Shared.Responses;

namespace Api.Controllers;

/// <summary>
/// Controller for managing duplicate file groups.
/// </summary>
[ApiController]
[Route("api/duplicates")]
[Produces("application/json")]
public class DuplicateGroupsController : ControllerBase
{
    private readonly IDuplicateService _service;
    private readonly ILogger<DuplicateGroupsController> _logger;

    public DuplicateGroupsController(IDuplicateService service, ILogger<DuplicateGroupsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Get all duplicate groups with pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<DuplicateGroupDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<DuplicateGroupDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        var result = await _service.GetGroupsAsync(page, pageSize, status, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a duplicate group with all its files.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DuplicateGroupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DuplicateGroupDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _service.GetGroupAsync(id, ct);

        if (result is null)
            return NotFound(ApiErrorResponse.NotFound($"Duplicate group with ID {id} not found"));

        return Ok(result);
    }

    /// <summary>
    /// Set a file as the original in a duplicate group.
    /// </summary>
    [HttpPut("{id:guid}/original")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetOriginal(
        Guid id,
        [FromBody] SetOriginalRequest request,
        CancellationToken ct = default)
    {
        var success = await _service.SetOriginalAsync(id, request.FileId, ct);

        if (!success)
            return NotFound(ApiErrorResponse.NotFound($"Duplicate group {id} or file {request.FileId} not found"));

        return Ok();
    }

    /// <summary>
    /// Auto-select the original file based on rules.
    /// </summary>
    [HttpPost("{id:guid}/auto-select")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AutoSelect(
        Guid id,
        [FromBody] AutoSelectRequest? request,
        CancellationToken ct = default)
    {
        request ??= new AutoSelectRequest();

        var selectedFileId = await _service.AutoSelectOriginalAsync(id, request, ct);

        if (selectedFileId is null)
            return NotFound(ApiErrorResponse.NotFound($"Duplicate group with ID {id} not found"));

        return Ok(new { originalFileId = selectedFileId });
    }

    /// <summary>
    /// Auto-select originals for all unresolved duplicate groups.
    /// </summary>
    [HttpPost("auto-select-all")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> AutoSelectAll(
        [FromBody] AutoSelectRequest? request,
        CancellationToken ct = default)
    {
        request ??= new AutoSelectRequest();

        var count = await _service.AutoSelectAllAsync(request, ct);

        return Ok(new { groupsProcessed = count });
    }

    /// <summary>
    /// Get duplicate statistics.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(FileStatisticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FileStatisticsDto>> GetStatistics(CancellationToken ct = default)
    {
        var result = await _service.GetStatisticsAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// Queue non-original files for deletion.
    /// </summary>
    [HttpDelete("{id:guid}/non-originals")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteNonOriginals(Guid id, CancellationToken ct = default)
    {
        var count = await _service.QueueNonOriginalsForDeletionAsync(id, ct);

        if (count == 0)
            return NotFound(ApiErrorResponse.NotFound($"Duplicate group with ID {id} not found or has no duplicates"));

        return Ok(new { filesQueued = count });
    }

    /// <summary>
    /// Validate duplicate groups (confirm kept file selections).
    /// </summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateDuplicates(
        [FromBody] ValidateDuplicatesRequest request,
        CancellationToken ct = default)
    {
        var count = await _service.ValidateDuplicatesAsync(request, ct);
        return Ok(new { validated = count });
    }

    /// <summary>
    /// Batch validate auto-selected duplicate groups.
    /// </summary>
    [HttpPost("validate-batch")]
    [ProducesResponseType(typeof(ValidateBatchResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ValidateBatchResponse>> ValidateBatch(
        [FromBody] ValidateBatchRequest request,
        CancellationToken ct = default)
    {
        var result = await _service.ValidateBatchAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Undo validation for duplicate groups.
    /// </summary>
    [HttpPost("undo-validation")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> UndoValidation(
        [FromBody] UndoValidationRequest request,
        CancellationToken ct = default)
    {
        var count = await _service.UndoValidationAsync(request, ct);
        return Ok(new { undone = count });
    }
}
