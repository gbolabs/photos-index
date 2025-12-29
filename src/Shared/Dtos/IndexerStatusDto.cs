using System.Text.Json.Serialization;

namespace Shared.Dtos;

/// <summary>
/// Status information for a connected indexer.
/// </summary>
public record IndexerStatusDto
{
    public required string IndexerId { get; init; }
    public required string Hostname { get; init; }
    public string? Version { get; init; }
    public string? CommitHash { get; init; }
    public string? Environment { get; init; }
    public IndexerState State { get; init; }
    public string? CurrentDirectory { get; init; }
    public string? CurrentActivity { get; init; }
    public int FilesProcessed { get; init; }
    public int FilesTotal { get; init; }
    public int ErrorCount { get; init; }
    public DateTime? LastScanStarted { get; init; }
    public DateTime? LastScanCompleted { get; init; }
    public DateTime ConnectedAt { get; init; }
    public DateTime LastHeartbeat { get; init; }
    public TimeSpan Uptime { get; init; }
    public string? LastError { get; init; }

    // Progress metrics
    public double FilesPerSecond { get; init; }
    public long BytesProcessed { get; init; }
    public long BytesTotal { get; init; }
    public double BytesPerSecond { get; init; }
    public int? EstimatedSecondsRemaining { get; init; }
    public double ProgressPercentage { get; init; }

    // Queue information
    public IReadOnlyList<ScanQueueItemDto>? ScanQueue { get; init; }
    public int QueuedDirectories { get; init; }
}

/// <summary>
/// Represents a directory in the scan queue.
/// </summary>
public record ScanQueueItemDto
{
    public required string DirectoryPath { get; init; }
    public int? EstimatedFileCount { get; init; }
    public int Priority { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IndexerState
{
    Idle,
    Scanning,
    Processing,
    Reprocessing,
    Paused,
    Error,
    Disconnected
}
