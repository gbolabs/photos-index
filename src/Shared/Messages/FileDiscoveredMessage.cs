namespace Shared.Messages;

/// <summary>
/// Message published when a file is discovered and uploaded to object storage.
/// Published by the API service after initial file upload.
/// </summary>
public record FileDiscoveredMessage
{
    /// <summary>
    /// Correlation ID for tracking the file through the entire processing pipeline.
    /// </summary>
    public Guid CorrelationId { get; init; }

    /// <summary>
    /// The ID of the indexed file record in the database.
    /// </summary>
    public Guid IndexedFileId { get; init; }

    /// <summary>
    /// The ID of the scan directory this file belongs to.
    /// </summary>
    public Guid ScanDirectoryId { get; init; }

    /// <summary>
    /// Original file path from the source system.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// SHA256 hash of the file contents.
    /// </summary>
    public string FileHash { get; init; } = string.Empty;

    /// <summary>
    /// Size of the file in bytes.
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    /// Object storage key where the file is stored (e.g., "files/{hash}").
    /// </summary>
    public string ObjectKey { get; init; } = string.Empty;
}
