namespace Shared.Dtos;

/// <summary>
/// Aggregated version information for all services in the system.
/// </summary>
public class SystemVersionsDto
{
    /// <summary>
    /// API service version info.
    /// </summary>
    public required ServiceVersionDto Api { get; set; }

    /// <summary>
    /// Web/SPA version info.
    /// </summary>
    public ServiceVersionDto? Web { get; set; }

    /// <summary>
    /// Connected indexer service versions.
    /// </summary>
    public IReadOnlyList<ServiceVersionDto> Indexers { get; set; } = [];

    /// <summary>
    /// Thumbnail service version info.
    /// </summary>
    public ServiceVersionDto? ThumbnailService { get; set; }

    /// <summary>
    /// Metadata service version info.
    /// </summary>
    public ServiceVersionDto? MetadataService { get; set; }
}

/// <summary>
/// Version information for a single service.
/// </summary>
public class ServiceVersionDto
{
    /// <summary>
    /// Service name identifier.
    /// </summary>
    public required string ServiceName { get; set; }

    /// <summary>
    /// Semantic version (e.g., "0.5.2").
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Git commit hash (short form).
    /// </summary>
    public string? CommitHash { get; set; }

    /// <summary>
    /// Git branch name.
    /// </summary>
    public string? Branch { get; set; }

    /// <summary>
    /// Build timestamp.
    /// </summary>
    public string? BuildTime { get; set; }

    /// <summary>
    /// Whether the service is currently available.
    /// </summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// Service uptime.
    /// </summary>
    public string? Uptime { get; set; }

    /// <summary>
    /// Instance identifier (for services with multiple instances).
    /// </summary>
    public string? InstanceId { get; set; }
}
