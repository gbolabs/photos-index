using Microsoft.AspNetCore.SignalR;
using Shared.Dtos;

namespace Api.Hubs;

/// <summary>
/// SignalR hub for communication between API, ThumbnailService, and UI.
/// - ThumbnailService connects and reports status
/// - UI clients join the "ui" group to receive progress updates
/// </summary>
public class ThumbnailServiceHub : Hub<IThumbnailServiceClient>
{
    private readonly ILogger<ThumbnailServiceHub> _logger;
    private static readonly Dictionary<string, ThumbnailServiceConnectionInfo> _connectedServices = new();
    private static readonly object _lock = new();

    public ThumbnailServiceHub(ILogger<ThumbnailServiceHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var serviceId = Context.GetHttpContext()?.Request.Query["serviceId"].ToString()
            ?? Context.ConnectionId;
        var hostname = Context.GetHttpContext()?.Request.Query["hostname"].ToString()
            ?? "unknown";

        // Only track if serviceId query param is provided
        if (!string.IsNullOrEmpty(Context.GetHttpContext()?.Request.Query["serviceId"]))
        {
            lock (_lock)
            {
                _connectedServices[Context.ConnectionId] = new ThumbnailServiceConnectionInfo(
                    serviceId,
                    hostname,
                    DateTime.UtcNow);
            }

            _logger.LogInformation("ThumbnailService connected: {ServiceId} from {Hostname}", serviceId, hostname);

            // Notify UI clients
            await Clients.Group("ui").ServiceConnected(serviceId, hostname);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        ThumbnailServiceConnectionInfo? connection = null;

        lock (_lock)
        {
            if (_connectedServices.TryGetValue(Context.ConnectionId, out connection))
            {
                _connectedServices.Remove(Context.ConnectionId);
            }
        }

        if (connection is not null)
        {
            _logger.LogInformation("ThumbnailService disconnected: {ServiceId}", connection.ServiceId);
            await Clients.Group("ui").ServiceDisconnected(connection.ServiceId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Called by ThumbnailService to report its status.
    /// </summary>
    public async Task ReportStatus(ThumbnailServiceStatusDto status)
    {
        lock (_lock)
        {
            if (_connectedServices.TryGetValue(Context.ConnectionId, out var connection))
            {
                _connectedServices[Context.ConnectionId] = connection with { LastStatus = status };
            }
        }

        // Notify UI clients
        await Clients.Group("ui").StatusUpdated(status);
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
    /// Request status from all connected services.
    /// </summary>
    public async Task RequestAllStatuses()
    {
        await Clients.All.RequestStatus();
    }

    public static IReadOnlyCollection<ThumbnailServiceStatusDto> GetConnectedServiceStatuses()
    {
        lock (_lock)
        {
            return _connectedServices.Values
                .Select(c => c.LastStatus ?? new ThumbnailServiceStatusDto
                {
                    ServiceId = c.ServiceId,
                    Hostname = c.Hostname,
                    State = ThumbnailServiceState.Idle,
                    ConnectedAt = c.ConnectedAt,
                    LastHeartbeat = c.ConnectedAt
                })
                .ToList();
        }
    }

    public static int GetConnectedServiceCount()
    {
        lock (_lock)
        {
            return _connectedServices.Count;
        }
    }

    public static bool HasConnectedService()
    {
        lock (_lock)
        {
            return _connectedServices.Count > 0;
        }
    }
}

/// <summary>
/// Client interface for ThumbnailService SignalR communication.
/// </summary>
public interface IThumbnailServiceClient
{
    Task RequestStatus();
    Task ServiceConnected(string serviceId, string hostname);
    Task ServiceDisconnected(string serviceId);
    Task StatusUpdated(ThumbnailServiceStatusDto status);
}

public record ThumbnailServiceConnectionInfo(
    string ServiceId,
    string Hostname,
    DateTime ConnectedAt,
    ThumbnailServiceStatusDto? LastStatus = null);
