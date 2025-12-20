namespace IndexingService.Models;

/// <summary>
/// Extracted metadata from an image file.
/// </summary>
public record ImageMetadata
{
    /// <summary>
    /// Image width in pixels.
    /// </summary>
    public int? Width { get; init; }

    /// <summary>
    /// Image height in pixels.
    /// </summary>
    public int? Height { get; init; }

    /// <summary>
    /// Date the photo was taken (from EXIF).
    /// </summary>
    public DateTime? DateTaken { get; init; }

    /// <summary>
    /// Camera manufacturer.
    /// </summary>
    public string? CameraMake { get; init; }

    /// <summary>
    /// Camera model.
    /// </summary>
    public string? CameraModel { get; init; }

    /// <summary>
    /// GPS latitude.
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>
    /// GPS longitude.
    /// </summary>
    public double? Longitude { get; init; }

    /// <summary>
    /// Image orientation from EXIF.
    /// </summary>
    public int? Orientation { get; init; }

    /// <summary>
    /// Whether metadata extraction was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if extraction failed.
    /// </summary>
    public string? Error { get; init; }

    public static ImageMetadata Failed(string error) => new()
    {
        Success = false,
        Error = error
    };
}
