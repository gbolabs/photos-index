namespace Shared.Messages;

/// <summary>
/// Message published when a thumbnail has been generated for a file.
/// Published by the ThumbnailService after thumbnail generation.
/// </summary>
public record ThumbnailGeneratedMessage
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
    /// Object storage key where the original file is stored.
    /// </summary>
    public string OriginalObjectKey { get; init; } = string.Empty;

    /// <summary>
    /// Object storage key where the thumbnail is stored.
    /// </summary>
    public string ThumbnailObjectKey { get; init; } = string.Empty;

    /// <summary>
    /// Width of the thumbnail in pixels.
    /// </summary>
    public int ThumbnailWidth { get; init; }

    /// <summary>
    /// Height of the thumbnail in pixels.
    /// </summary>
    public int ThumbnailHeight { get; init; }

    /// <summary>
    /// Size of the thumbnail file in bytes.
    /// </summary>
    public long ThumbnailSize { get; init; }
}
