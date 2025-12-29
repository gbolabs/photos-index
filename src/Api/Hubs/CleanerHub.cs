using Microsoft.AspNetCore.SignalR;
using Shared.Dtos;

namespace Api.Hubs;

/// <summary>
/// SignalR hub for communication between API, CleanerService, and UI.
/// - CleanerService connects and receives delete commands
/// - UI clients join the "ui" group to receive progress updates
/// </summary>
public class CleanerHub : Hub<ICleanerClient>
{
    private readonly ILogger<CleanerHub> _logger;
    private static readonly Dictionary<string, CleanerConnectionInfo> _connectedCleaners = new();
    private static readonly object _lock = new();

    public CleanerHub(ILogger<CleanerHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var cleanerId = Context.GetHttpContext()?.Request.Query["cleanerId"].ToString()
            ?? Context.ConnectionId;
        var hostname = Context.GetHttpContext()?.Request.Query["hostname"].ToString()
            ?? "unknown";

        // Only track as cleaner if cleanerId query param is provided
        if (!string.IsNullOrEmpty(Context.GetHttpContext()?.Request.Query["cleanerId"]))
        {
            lock (_lock)
            {
                _connectedCleaners[Context.ConnectionId] = new CleanerConnectionInfo(
                    cleanerId,
                    hostname,
                    DateTime.UtcNow);
            }

            _logger.LogInformation("Cleaner connected: {CleanerId} from {Hostname}", cleanerId, hostname);

            // Notify UI clients
            await Clients.Group("ui").CleanerConnected(cleanerId, hostname);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        CleanerConnectionInfo? connection = null;

        lock (_lock)
        {
            if (_connectedCleaners.TryGetValue(Context.ConnectionId, out connection))
            {
                _connectedCleaners.Remove(Context.ConnectionId);
            }
        }

        if (connection is not null)
        {
            _logger.LogInformation("Cleaner disconnected: {CleanerId}", connection.CleanerId);
            await Clients.Group("ui").CleanerDisconnected(connection.CleanerId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Called by CleanerService to report its status.
    /// </summary>
    public async Task ReportStatus(CleanerStatusDto status)
    {
        lock (_lock)
        {
            if (_connectedCleaners.TryGetValue(Context.ConnectionId, out var connection))
            {
                _connectedCleaners[Context.ConnectionId] = connection with { LastStatus = status };
            }
        }

        // Notify UI clients
        await Clients.Group("ui").CleanerStatusUpdated(status);
    }

    /// <summary>
    /// Called by CleanerService when a file delete starts.
    /// </summary>
    public async Task ReportDeleteProgress(Guid jobId, Guid fileId, string status)
    {
        await Clients.Group("ui").DeleteProgress(jobId, fileId, status);
    }

    /// <summary>
    /// Called by CleanerService when a file delete completes.
    /// </summary>
    public async Task ReportDeleteComplete(DeleteFileResult result)
    {
        _logger.LogInformation("Delete complete: Job={JobId}, File={FileId}, Success={Success}",
            result.JobId, result.FileId, result.Success);
        await Clients.Group("ui").DeleteComplete(result);
    }

    /// <summary>
    /// Called by CleanerService when a job completes.
    /// </summary>
    public async Task ReportJobComplete(Guid jobId, int succeeded, int failed, int skipped)
    {
        _logger.LogInformation("Job complete: {JobId}, Succeeded={Succeeded}, Failed={Failed}, Skipped={Skipped}",
            jobId, succeeded, failed, skipped);
        await Clients.Group("ui").JobComplete(jobId, succeeded, failed, skipped);
    }

    /// <summary>
    /// UI clients call this to join the UI group for receiving updates.
    /// </summary>
    public async Task JoinUIGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "ui");
        _logger.LogDebug("Client {ConnectionId} joined UI group", Context.ConnectionId);
    }

    /// <summary>
    /// Request status from all connected cleaners.
    /// </summary>
    public async Task RequestAllStatuses()
    {
        await Clients.All.RequestStatus();
    }

    public static IReadOnlyCollection<CleanerStatusDto> GetConnectedCleanerStatuses()
    {
        lock (_lock)
        {
            return _connectedCleaners.Values
                .Select(c => c.LastStatus ?? new CleanerStatusDto
                {
                    CleanerId = c.CleanerId,
                    Hostname = c.Hostname,
                    State = CleanerState.Idle,
                    DryRunEnabled = true,
                    ConnectedAt = c.ConnectedAt,
                    LastHeartbeat = c.ConnectedAt
                })
                .ToList();
        }
    }

    public static int GetConnectedCleanerCount()
    {
        lock (_lock)
        {
            return _connectedCleaners.Count;
        }
    }

    public static bool HasConnectedCleaner()
    {
        lock (_lock)
        {
            return _connectedCleaners.Count > 0;
        }
    }
}

/// <summary>
/// Extended client interface with methods for UI notifications.
/// </summary>
public interface ICleanerClient
{
    Task DeleteFile(DeleteFileRequest request);
    Task DeleteFiles(IEnumerable<DeleteFileRequest> requests);
    Task CancelJob(Guid jobId);
    Task SetDryRun(bool enabled);
    Task RequestStatus();

    // UI notification methods
    Task CleanerConnected(string cleanerId, string hostname);
    Task CleanerDisconnected(string cleanerId);
    Task CleanerStatusUpdated(CleanerStatusDto status);
    Task DeleteProgress(Guid jobId, Guid fileId, string status);
    Task DeleteComplete(DeleteFileResult result);
    Task JobComplete(Guid jobId, int succeeded, int failed, int skipped);
}

public record CleanerConnectionInfo(
    string CleanerId,
    string Hostname,
    DateTime ConnectedAt,
    CleanerStatusDto? LastStatus = null);
