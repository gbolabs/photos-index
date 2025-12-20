namespace IndexingService.Models;

/// <summary>
/// Options for thumbnail generation.
/// </summary>
public record ThumbnailOptions
{
    /// <summary>
    /// Maximum width of the thumbnail.
    /// </summary>
    public int MaxWidth { get; init; } = 200;

    /// <summary>
    /// Maximum height of the thumbnail.
    /// </summary>
    public int MaxHeight { get; init; } = 200;

    /// <summary>
    /// JPEG quality (1-100).
    /// </summary>
    public int Quality { get; init; } = 75;

    /// <summary>
    /// Whether to preserve aspect ratio.
    /// </summary>
    public bool PreserveAspectRatio { get; init; } = true;
}
