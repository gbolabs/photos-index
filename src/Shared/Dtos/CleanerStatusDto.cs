using System.Text.Json.Serialization;

namespace Shared.Dtos;

/// <summary>
/// Status information for a connected cleaner service.
/// </summary>
public record CleanerStatusDto
{
    public required string CleanerId { get; init; }
    public required string Hostname { get; init; }
    public string? Version { get; init; }
    public string? CommitHash { get; init; }
    public string? Environment { get; init; }
    public CleanerState State { get; init; }
    public bool DryRunEnabled { get; init; }
    public string? CurrentActivity { get; init; }
    public int FilesProcessed { get; init; }
    public int FilesTotal { get; init; }
    public int FilesDeleted { get; init; }
    public int FilesFailed { get; init; }
    public int FilesSkipped { get; init; }
    public long BytesArchived { get; init; }
    public int ErrorCount { get; init; }
    public DateTime? LastJobStarted { get; init; }
    public DateTime? LastJobCompleted { get; init; }
    public DateTime ConnectedAt { get; init; }
    public DateTime LastHeartbeat { get; init; }
    public TimeSpan Uptime { get; init; }
    public string? LastError { get; init; }
    public Guid? CurrentJobId { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CleanerState
{
    Idle,
    Processing,
    Uploading,
    Deleting,
    Paused,
    Error,
    Disconnected
}
