using IndexingService.Models;

namespace IndexingService.Services;

/// <summary>
/// Service for scanning directories for image files.
/// </summary>
public interface IFileScanner
{
    /// <summary>
    /// Scans a directory for image files.
    /// </summary>
    /// <param name="directoryPath">The directory to scan.</param>
    /// <param name="includeSubdirectories">Whether to scan subdirectories recursively.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <returns>Async enumerable of scanned files.</returns>
    IAsyncEnumerable<ScannedFile> ScanAsync(
        string directoryPath,
        bool includeSubdirectories,
        CancellationToken cancellationToken,
        IProgress<ScanProgress>? progress = null);
}
