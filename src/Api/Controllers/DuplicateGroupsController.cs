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
    private readonly DuplicateScanBackgroundService _scanService;
    private readonly ILogger<DuplicateGroupsController> _logger;

    public DuplicateGroupsController(
        IDuplicateService service,
        DuplicateScanBackgroundService scanService,
        ILogger<DuplicateGroupsController> logger)
    {
        _service = service;
        _scanService = scanService;
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

    /// <summary>
    /// Queue a duplicate scan job (async).
    /// </summary>
    /// <remarks>
    /// Queues a background job to scan all indexed files for duplicates.
    /// Returns immediately with a job ID. Monitor progress via SignalR or the status endpoint.
    /// </remarks>
    [HttpPost("scan")]
    [ProducesResponseType(typeof(object), StatusCodes.Status202Accepted)]
    public IActionResult QueueScanJob()
    {
        _logger.LogInformation("Duplicate scan job requested via API");
        var jobId = _scanService.QueueScanJob();
        return Accepted(new { jobId, message = "Scan job queued. Monitor progress via SignalR 'DuplicateScanProgress' event." });
    }

    /// <summary>
    /// Get status of a duplicate scan job.
    /// </summary>
    [HttpGet("scan/{jobId}")]
    [ProducesResponseType(typeof(DuplicateScanJob), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public ActionResult<DuplicateScanJob> GetScanJobStatus(string jobId)
    {
        var job = _scanService.GetJobStatus(jobId);
        if (job is null)
            return NotFound(ApiErrorResponse.NotFound($"Scan job {jobId} not found"));
        return Ok(job);
    }

    /// <summary>
    /// Get recent duplicate scan jobs.
    /// </summary>
    [HttpGet("scan/jobs")]
    [ProducesResponseType(typeof(IEnumerable<DuplicateScanJob>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<DuplicateScanJob>> GetRecentJobs()
    {
        return Ok(_scanService.GetRecentJobs());
    }

    /// <summary>
    /// Synchronously scan for duplicates (for small collections).
    /// </summary>
    /// <remarks>
    /// Runs duplicate detection synchronously. May timeout on large collections.
    /// For large collections, use POST /scan to queue an async job.
    /// </remarks>
    [HttpPost("scan/sync")]
    [ProducesResponseType(typeof(DuplicateScanResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DuplicateScanResultDto>> ScanForDuplicatesSync(CancellationToken ct = default)
    {
        _logger.LogInformation("Synchronous duplicate scan requested via API");
        var result = await _service.ScanForDuplicatesAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// Get the directory pattern for a duplicate group.
    /// </summary>
    /// <remarks>
    /// Returns information about the directory pattern (unique set of parent directories)
    /// and how many other groups share the same pattern. Useful for batch operations.
    /// </remarks>
    [HttpGet("{id:guid}/pattern")]
    [ProducesResponseType(typeof(DirectoryPatternDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DirectoryPatternDto>> GetPattern(
        Guid id,
        CancellationToken ct = default)
    {
        var pattern = await _service.GetPatternForGroupAsync(id, ct);

        if (pattern is null)
            return NotFound(ApiErrorResponse.NotFound($"Duplicate group with ID {id} not found"));

        return Ok(pattern);
    }

    /// <summary>
    /// Apply a pattern rule to select originals across all matching groups.
    /// </summary>
    /// <remarks>
    /// Finds all duplicate groups that share the exact same directory pattern
    /// and sets the original file to be from the specified preferred directory.
    /// Returns the ID of the next unresolved group with a different pattern
    /// for easy navigation.
    /// </remarks>
    [HttpPost("patterns/apply")]
    [ProducesResponseType(typeof(ApplyPatternRuleResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApplyPatternRuleResultDto>> ApplyPatternRule(
        [FromBody] ApplyPatternRuleRequest request,
        CancellationToken ct = default)
    {
        var result = await _service.ApplyPatternRuleAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get navigation info for moving between duplicate groups.
    /// </summary>
    /// <remarks>
    /// Returns the IDs of the previous and next groups in the list,
    /// as well as the current position and total count.
    /// </remarks>
    [HttpGet("{id:guid}/navigation")]
    [ProducesResponseType(typeof(GroupNavigationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GroupNavigationDto>> GetNavigation(
        Guid id,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var result = await _service.GetNavigationAsync(id, status, ct);
        return Ok(result);
    }

    // Session endpoints for keyboard-driven review

    /// <summary>
    /// Start or resume a keyboard review session.
    /// </summary>
    /// <remarks>
    /// Creates a new session or resumes an existing active/paused session.
    /// Session state is persisted to survive restarts.
    /// </remarks>
    [HttpPost("session/start")]
    [ProducesResponseType(typeof(SelectionSessionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SelectionSessionDto>> StartSession(
        [FromBody] StartSessionRequest? request,
        CancellationToken ct = default)
    {
        request ??= new StartSessionRequest();
        var session = await _service.StartOrResumeSessionAsync(request.ResumeExisting, ct);
        return Ok(session);
    }

    /// <summary>
    /// Get the current active session.
    /// </summary>
    [HttpGet("session/current")]
    [ProducesResponseType(typeof(SelectionSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<SelectionSessionDto>> GetCurrentSession(CancellationToken ct = default)
    {
        var session = await _service.GetCurrentSessionAsync(ct);
        if (session is null)
            return NoContent();
        return Ok(session);
    }

    /// <summary>
    /// Pause the current session.
    /// </summary>
    [HttpPost("session/{sessionId:guid}/pause")]
    [ProducesResponseType(typeof(SelectionSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SelectionSessionDto>> PauseSession(
        Guid sessionId,
        CancellationToken ct = default)
    {
        try
        {
            var session = await _service.PauseSessionAsync(sessionId, ct);
            return Ok(session);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiErrorResponse.NotFound(ex.Message));
        }
    }

    /// <summary>
    /// Get session progress.
    /// </summary>
    [HttpGet("session/{sessionId:guid}/progress")]
    [ProducesResponseType(typeof(SessionProgressDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SessionProgressDto>> GetSessionProgress(
        Guid sessionId,
        CancellationToken ct = default)
    {
        var progress = await _service.GetSessionProgressAsync(sessionId, ct);
        return Ok(progress);
    }

    /// <summary>
    /// Propose a file as original for a duplicate group (keyboard shortcut: Space).
    /// </summary>
    /// <remarks>
    /// Sets the specified file as the proposed original but does not validate.
    /// Returns the next unreviewed group ID for navigation.
    /// </remarks>
    [HttpPost("{id:guid}/propose")]
    [ProducesResponseType(typeof(ReviewActionResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReviewActionResultDto>> ProposeOriginal(
        Guid id,
        [FromBody] ProposeOriginalRequest request,
        CancellationToken ct = default)
    {
        var result = await _service.ProposeOriginalAsync(id, request.FileId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Validate the current group selection (keyboard shortcut: Enter).
    /// </summary>
    /// <remarks>
    /// Validates the proposed/selected original and advances to the next group.
    /// </remarks>
    [HttpPost("{id:guid}/validate-selection")]
    [ProducesResponseType(typeof(ReviewActionResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReviewActionResultDto>> ValidateSelection(
        Guid id,
        CancellationToken ct = default)
    {
        var result = await _service.ValidateGroupAsync(id, ct);
        return Ok(result);
    }

    /// <summary>
    /// Skip the current group (keyboard shortcut: S).
    /// </summary>
    /// <remarks>
    /// Skips the group without making a selection and advances to the next group.
    /// </remarks>
    [HttpPost("{id:guid}/skip")]
    [ProducesResponseType(typeof(ReviewActionResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReviewActionResultDto>> SkipGroup(
        Guid id,
        CancellationToken ct = default)
    {
        var result = await _service.SkipGroupAsync(id, ct);
        return Ok(result);
    }

    /// <summary>
    /// Undo the last action on a group (keyboard shortcut: U).
    /// </summary>
    /// <remarks>
    /// Reverts the group to pending status and resets the kept file selection.
    /// </remarks>
    [HttpPost("{id:guid}/undo")]
    [ProducesResponseType(typeof(ReviewActionResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReviewActionResultDto>> UndoAction(
        Guid id,
        CancellationToken ct = default)
    {
        var result = await _service.UndoLastActionAsync(id, ct);
        return Ok(result);
    }
}
