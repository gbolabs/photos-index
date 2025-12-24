namespace Shared.Messages;

/// <summary>
/// Message published when metadata has been extracted from a file.
/// Published by the MetadataService after EXIF/metadata extraction.
/// </summary>
public record MetadataExtractedMessage
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
    /// Object storage key where the file is stored.
    /// </summary>
    public string ObjectKey { get; init; } = string.Empty;

    /// <summary>
    /// Width of the image in pixels.
    /// </summary>
    public int? Width { get; init; }

    /// <summary>
    /// Height of the image in pixels.
    /// </summary>
    public int? Height { get; init; }

    /// <summary>
    /// Date and time when the photo was taken (from EXIF data).
    /// </summary>
    public DateTime? DateTaken { get; init; }

    /// <summary>
    /// Camera make (from EXIF data).
    /// </summary>
    public string? CameraMake { get; init; }

    /// <summary>
    /// Camera model (from EXIF data).
    /// </summary>
    public string? CameraModel { get; init; }

    /// <summary>
    /// GPS latitude (from EXIF data).
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>
    /// GPS longitude (from EXIF data).
    /// </summary>
    public double? Longitude { get; init; }

    /// <summary>
    /// Image orientation (from EXIF data).
    /// </summary>
    public int? Orientation { get; init; }
}
