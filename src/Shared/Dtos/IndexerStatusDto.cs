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
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IndexerState
{
    Idle,
    Scanning,
    Processing,
    Reprocessing,
    Error,
    Disconnected
}
