using IndexingService.Models;

namespace IndexingService.Services;

/// <summary>
/// Service for extracting metadata and generating thumbnails from images.
/// </summary>
public interface IMetadataExtractor
{
    /// <summary>
    /// Extracts metadata from an image file.
    /// </summary>
    /// <param name="filePath">Path to the image file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extracted metadata.</returns>
    Task<ImageMetadata> ExtractAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>
    /// Generates a thumbnail for an image.
    /// </summary>
    /// <param name="filePath">Path to the image file.</param>
    /// <param name="options">Thumbnail options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Thumbnail as JPEG bytes, or null if generation failed.</returns>
    Task<byte[]?> GenerateThumbnailAsync(
        string filePath,
        ThumbnailOptions options,
        CancellationToken cancellationToken);
}
