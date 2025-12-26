using Api.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Shared.Dtos;

namespace Api.Controllers;

/// <summary>
/// Controller for managing and monitoring connected indexers.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class IndexersController : ControllerBase
{
    private readonly IHubContext<IndexerHub> _hubContext;

    public IndexersController(IHubContext<IndexerHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <summary>
    /// Get list of currently connected indexers with their status.
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<IndexerStatusDto>> GetConnectedIndexers()
    {
        return Ok(IndexerHub.GetConnectedIndexerStatuses());
    }

    /// <summary>
    /// Get count of connected indexers.
    /// </summary>
    [HttpGet("count")]
    public ActionResult<int> GetConnectedIndexersCount()
    {
        return Ok(IndexerHub.GetConnectedIndexerCount());
    }

    /// <summary>
    /// Request all connected indexers to report their status.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult> RefreshStatuses()
    {
        await _hubContext.Clients.All.SendAsync("RequestStatus");
        return Ok();
    }

    /// <summary>
    /// Trigger an immediate scan on all connected indexers.
    /// </summary>
    /// <param name="directoryId">Optional directory ID to scan only that directory.</param>
    [HttpPost("scan")]
    public async Task<ActionResult<ScanTriggerResult>> TriggerScan([FromQuery] Guid? directoryId = null)
    {
        var connectedCount = IndexerHub.GetConnectedIndexerCount();
        if (connectedCount == 0)
        {
            return BadRequest(new ScanTriggerResult(false, "No indexers connected"));
        }

        await _hubContext.Clients.All.SendAsync("StartScan", directoryId);
        return Ok(new ScanTriggerResult(true, $"Scan triggered on {connectedCount} indexer(s)"));
    }
}

public record ScanTriggerResult(bool Success, string Message);
