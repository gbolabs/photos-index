using Microsoft.AspNetCore.SignalR;
using Shared.Dtos;

namespace Api.Hubs;

/// <summary>
/// Interface for strongly-typed indexer client methods.
/// Used by ReprocessService to send commands to connected indexers.
/// </summary>
public interface IIndexerClient
{
    Task ReprocessFile(Guid fileId, string filePath);
    Task ReprocessFiles(IEnumerable<ReprocessFileRequest> files);
    Task RequestPreview(Guid fileId, string filePath);
}

/// <summary>
/// SignalR hub for communication between API, Indexer, and UI.
/// - Indexers connect and receive reprocess commands
/// - UI clients join the "ui" group to receive progress updates
/// </summary>
public class IndexerHub : Hub
{
    private readonly ILogger<IndexerHub> _logger;
    private static readonly Dictionary<string, IndexerConnectionInfo> _connectedIndexers = new();
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
                _connectedIndexers[Context.ConnectionId] = new IndexerConnectionInfo(
                    indexerId,
                    hostname,
                    DateTime.UtcNow);
            }

            _logger.LogInformation("Indexer connected: {IndexerId} from {Hostname}", indexerId, hostname);

            // Notify UI clients
            await Clients.Group("ui").SendAsync("IndexerConnected", indexerId, hostname);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        IndexerConnectionInfo? connection = null;

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
    /// Called by Indexer when preview is ready (uploaded to MinIO)
    /// </summary>
    public async Task ReportPreviewReady(Guid fileId, string previewUrl)
    {
        _logger.LogDebug("Preview ready for {FileId}: {Url}", fileId, previewUrl);
        await Clients.Group("ui").SendAsync("PreviewReady", fileId, previewUrl);
    }

    /// <summary>
    /// Called by Indexer when preview generation fails
    /// </summary>
    public async Task ReportPreviewFailed(Guid fileId, string error)
    {
        _logger.LogWarning("Preview failed for {FileId}: {Error}", fileId, error);
        await Clients.Group("ui").SendAsync("PreviewFailed", fileId, error);
    }

    /// <summary>
    /// UI clients call this to join the UI group for receiving updates
    /// </summary>
    public async Task JoinUIGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "ui");
        _logger.LogDebug("Client {ConnectionId} joined UI group", Context.ConnectionId);
    }

    /// <summary>
    /// Called by Indexer to report its current status
    /// </summary>
    public async Task ReportStatus(IndexerStatusDto status)
    {
        lock (_lock)
        {
            if (_connectedIndexers.TryGetValue(Context.ConnectionId, out var connection))
            {
                _connectedIndexers[Context.ConnectionId] = connection with { LastStatus = status };
            }
        }

        // Notify UI clients of status update
        await Clients.Group("ui").SendAsync("IndexerStatusUpdated", status);
    }

    /// <summary>
    /// Request status from all connected indexers
    /// </summary>
    public async Task RequestAllStatuses()
    {
        await Clients.All.SendAsync("RequestStatus");
    }

    /// <summary>
    /// Trigger a scan on all connected indexers or a specific one
    /// </summary>
    public async Task TriggerScan(Guid? directoryId = null)
    {
        _logger.LogInformation("Triggering scan: DirectoryId={DirectoryId}", directoryId?.ToString() ?? "all");
        await Clients.All.SendAsync("StartScan", directoryId);
        await Clients.Group("ui").SendAsync("ScanTriggered", directoryId);
    }

    /// <summary>
    /// Called by Indexer to report scan progress
    /// </summary>
    public async Task ReportScanProgress(string directoryPath, int filesProcessed, int filesTotal)
    {
        await Clients.Group("ui").SendAsync("ScanProgress", directoryPath, filesProcessed, filesTotal);
    }

    /// <summary>
    /// Called by Indexer to report scan completion
    /// </summary>
    public async Task ReportScanComplete(string directoryPath, int filesScanned, int filesIngested, int filesFailed)
    {
        await Clients.Group("ui").SendAsync("ScanComplete", directoryPath, filesScanned, filesIngested, filesFailed);
    }

    public static IReadOnlyCollection<IndexerStatusDto> GetConnectedIndexerStatuses()
    {
        lock (_lock)
        {
            return _connectedIndexers.Values
                .Select(c => c.LastStatus ?? new IndexerStatusDto
                {
                    IndexerId = c.IndexerId,
                    Hostname = c.Hostname,
                    State = IndexerState.Idle,
                    ConnectedAt = c.ConnectedAt,
                    LastHeartbeat = c.ConnectedAt
                })
                .ToList();
        }
    }

    public static int GetConnectedIndexerCount()
    {
        lock (_lock)
        {
            return _connectedIndexers.Count;
        }
    }
}

public record IndexerConnectionInfo(
    string IndexerId,
    string Hostname,
    DateTime ConnectedAt,
    IndexerStatusDto? LastStatus = null);

public record ReprocessFileRequest(Guid FileId, string FilePath);
