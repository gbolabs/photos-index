using System.Text.Json.Serialization;

namespace Shared.Dtos;

/// <summary>
/// Status information for a connected thumbnail service.
/// </summary>
public record ThumbnailServiceStatusDto
{
    public required string ServiceId { get; init; }
    public required string Hostname { get; init; }
    public string? Version { get; init; }
    public string? CommitHash { get; init; }
    public string? Environment { get; init; }
    public ThumbnailServiceState State { get; init; }
    public int ThumbnailsGenerated { get; init; }
    public int ThumbnailsFailed { get; init; }
    public int QueueSize { get; init; }
    public DateTime ConnectedAt { get; init; }
    public DateTime LastHeartbeat { get; init; }
    public TimeSpan Uptime { get; init; }
    public string? LastError { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ThumbnailServiceState
{
    Idle,
    Processing,
    Error,
    Disconnected
}
