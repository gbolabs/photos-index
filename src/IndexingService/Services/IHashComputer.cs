using IndexingService.Models;

namespace IndexingService.Services;

/// <summary>
/// Service for computing SHA256 hashes of files.
/// </summary>
public interface IHashComputer
{
    /// <summary>
    /// Computes the SHA256 hash of a file using streaming.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <returns>Hash result with the computed hash.</returns>
    Task<HashResult> ComputeAsync(
        string filePath,
        CancellationToken cancellationToken,
        IProgress<HashProgress>? progress = null);

    /// <summary>
    /// Computes hashes for multiple files in parallel.
    /// </summary>
    /// <param name="filePaths">Paths to the files.</param>
    /// <param name="maxParallelism">Maximum number of parallel operations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of hash results.</returns>
    IAsyncEnumerable<HashResult> ComputeBatchAsync(
        IEnumerable<string> filePaths,
        int maxParallelism,
        CancellationToken cancellationToken);
}
