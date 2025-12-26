using Api.Hubs;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Controller for managing and monitoring connected indexers.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class IndexersController : ControllerBase
{
    /// <summary>
    /// Get list of currently connected indexers.
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<IndexerConnection>> GetConnectedIndexers()
    {
        return Ok(IndexerHub.GetConnectedIndexers());
    }

    /// <summary>
    /// Get count of connected indexers.
    /// </summary>
    [HttpGet("count")]
    public ActionResult<int> GetConnectedIndexersCount()
    {
        return Ok(IndexerHub.GetConnectedIndexers().Count);
    }
}
