using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

/// <summary>
/// Interface for strongly-typed indexer client methods.
/// Used by ReprocessService to send commands to connected indexers.
/// </summary>
public interface IIndexerClient
{
    Task ReprocessFile(Guid fileId, string filePath);
    Task ReprocessFiles(IEnumerable<ReprocessFileRequest> files);
}

/// <summary>
/// SignalR hub for communication between API, Indexer, and UI.
/// - Indexers connect and receive reprocess commands
/// - UI clients join the "ui" group to receive progress updates
/// </summary>
public class IndexerHub : Hub
{
    private readonly ILogger<IndexerHub> _logger;
    private static readonly Dictionary<string, IndexerConnection> _connectedIndexers = new();
    private static readonly object _lock = new();

    public IndexerHub(ILogger<IndexerHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var indexerId = Context.GetHttpContext()?.Request.Query["indexerId"].ToString()
            ?? Context.ConnectionId;
        var hostname = Context.GetHttpContext()?.Request.Query["hostname"].ToString()
            ?? "unknown";

        // Only track as indexer if indexerId query param is provided
        if (!string.IsNullOrEmpty(Context.GetHttpContext()?.Request.Query["indexerId"]))
        {
            lock (_lock)
            {
                _connectedIndexers[Context.ConnectionId] = new IndexerConnection(indexerId, hostname);
            }

            _logger.LogInformation("Indexer connected: {IndexerId} from {Hostname}", indexerId, hostname);

            // Notify UI clients
            await Clients.Group("ui").SendAsync("IndexerConnected", indexerId, hostname);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        IndexerConnection? connection = null;

        lock (_lock)
        {
            if (_connectedIndexers.TryGetValue(Context.ConnectionId, out connection))
            {
                _connectedIndexers.Remove(Context.ConnectionId);
            }
        }

        if (connection is not null)
        {
            _logger.LogInformation("Indexer disconnected: {IndexerId}", connection.IndexerId);
            await Clients.Group("ui").SendAsync("IndexerDisconnected", connection.IndexerId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Called by Indexer to report progress during reprocessing
    /// </summary>
    public async Task ReportProgress(Guid fileId, string status)
    {
        await Clients.Group("ui").SendAsync("ReprocessProgress", fileId, status);
    }

    /// <summary>
    /// Called by Indexer when reprocessing is complete
    /// </summary>
    public async Task ReportComplete(Guid fileId, bool success, string? error)
    {
        await Clients.Group("ui").SendAsync("ReprocessComplete", fileId, success, error);
    }

    /// <summary>
    /// UI clients call this to join the UI group for receiving updates
    /// </summary>
    public async Task JoinUIGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "ui");
        _logger.LogDebug("Client {ConnectionId} joined UI group", Context.ConnectionId);
    }

    public static IReadOnlyCollection<IndexerConnection> GetConnectedIndexers()
    {
        lock (_lock)
        {
            return _connectedIndexers.Values.ToList();
        }
    }
}

public record IndexerConnection(string IndexerId, string Hostname);
public record ReprocessFileRequest(Guid FileId, string FilePath);
