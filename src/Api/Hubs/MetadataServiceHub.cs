using Microsoft.AspNetCore.SignalR;
using Shared.Dtos;

namespace Api.Hubs;

/// <summary>
/// SignalR hub for communication between API, MetadataService, and UI.
/// - MetadataService connects and reports status
/// - UI clients join the "ui" group to receive progress updates
/// </summary>
public class MetadataServiceHub : Hub<IMetadataServiceClient>
{
    private readonly ILogger<MetadataServiceHub> _logger;
    private static readonly Dictionary<string, MetadataServiceConnectionInfo> _connectedServices = new();
    private static readonly object _lock = new();

    public MetadataServiceHub(ILogger<MetadataServiceHub> logger)
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
                _connectedServices[Context.ConnectionId] = new MetadataServiceConnectionInfo(
                    serviceId,
                    hostname,
                    DateTime.UtcNow);
            }

            _logger.LogInformation("MetadataService connected: {ServiceId} from {Hostname}", serviceId, hostname);

            // Notify UI clients
            await Clients.Group("ui").ServiceConnected(serviceId, hostname);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        MetadataServiceConnectionInfo? connection = null;

        lock (_lock)
        {
            if (_connectedServices.TryGetValue(Context.ConnectionId, out connection))
            {
                _connectedServices.Remove(Context.ConnectionId);
            }
        }

        if (connection is not null)
        {
            _logger.LogInformation("MetadataService disconnected: {ServiceId}", connection.ServiceId);
            await Clients.Group("ui").ServiceDisconnected(connection.ServiceId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Called by MetadataService to report its status.
    /// </summary>
    public async Task ReportStatus(MetadataServiceStatusDto status)
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

    public static IReadOnlyCollection<MetadataServiceStatusDto> GetConnectedServiceStatuses()
    {
        lock (_lock)
        {
            return _connectedServices.Values
                .Select(c => c.LastStatus ?? new MetadataServiceStatusDto
                {
                    ServiceId = c.ServiceId,
                    Hostname = c.Hostname,
                    State = MetadataServiceState.Idle,
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
/// Client interface for MetadataService SignalR communication.
/// </summary>
public interface IMetadataServiceClient
{
    Task RequestStatus();
    Task ServiceConnected(string serviceId, string hostname);
    Task ServiceDisconnected(string serviceId);
    Task StatusUpdated(MetadataServiceStatusDto status);
}

public record MetadataServiceConnectionInfo(
    string ServiceId,
    string Hostname,
    DateTime ConnectedAt,
    MetadataServiceStatusDto? LastStatus = null);
