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
    /// Whether the metadata extraction was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if extraction failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

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
    public double? GpsLatitude { get; init; }

    /// <summary>
    /// GPS longitude (from EXIF data).
    /// </summary>
    public double? GpsLongitude { get; init; }

    /// <summary>
    /// ISO sensitivity (from EXIF data).
    /// </summary>
    public int? Iso { get; init; }

    /// <summary>
    /// Aperture value (from EXIF data), e.g., "f/2.8".
    /// </summary>
    public string? Aperture { get; init; }

    /// <summary>
    /// Shutter speed (from EXIF data), e.g., "1/125".
    /// </summary>
    public string? ShutterSpeed { get; init; }

    /// <summary>
    /// Image orientation (from EXIF data).
    /// </summary>
    public int? Orientation { get; init; }
}
