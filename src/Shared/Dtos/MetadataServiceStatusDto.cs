using System.Text.Json.Serialization;

namespace Shared.Dtos;

/// <summary>
/// Status information for a connected metadata service.
/// </summary>
public record MetadataServiceStatusDto
{
    public required string ServiceId { get; init; }
    public required string Hostname { get; init; }
    public string? Version { get; init; }
    public string? CommitHash { get; init; }
    public string? Environment { get; init; }
    public MetadataServiceState State { get; init; }
    public int FilesProcessed { get; init; }
    public int FilesFailed { get; init; }
    public int QueueSize { get; init; }
    public DateTime ConnectedAt { get; init; }
    public DateTime LastHeartbeat { get; init; }
    public TimeSpan Uptime { get; init; }
    public string? LastError { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MetadataServiceState
{
    Idle,
    Processing,
    Error,
    Disconnected
}
