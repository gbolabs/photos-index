namespace Shared.Dtos;

/// <summary>
/// Build and version information for the application.
/// </summary>
public class BuildInfoDto
{
    /// <summary>
    /// Service name (e.g., "photos-index-api", "photos-index-indexer").
    /// </summary>
    public required string ServiceName { get; set; }

    /// <summary>
    /// Application version from assembly.
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
    /// Build timestamp in UTC.
    /// </summary>
    public string? BuildTime { get; set; }

    /// <summary>
    /// .NET runtime version.
    /// </summary>
    public required string RuntimeVersion { get; set; }

    /// <summary>
    /// Environment (Development, Production, etc.).
    /// </summary>
    public string? Environment { get; set; }

    /// <summary>
    /// Service start time in UTC.
    /// </summary>
    public DateTime StartTimeUtc { get; set; }

    /// <summary>
    /// How long the service has been running.
    /// </summary>
    public string? Uptime { get; set; }
}
