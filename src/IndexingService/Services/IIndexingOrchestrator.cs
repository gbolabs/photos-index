using IndexingService.Models;

namespace IndexingService.Services;

/// <summary>
/// Orchestrates the indexing process by coordinating scanning, hashing, metadata extraction, and API ingestion.
/// </summary>
public interface IIndexingOrchestrator
{
    /// <summary>
    /// Runs a full indexing cycle for all enabled scan directories.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of completed indexing jobs.</returns>
    Task<IReadOnlyList<IndexingJob>> RunIndexingCycleAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Indexes a specific directory.
    /// </summary>
    /// <param name="directoryId">ID of the scan directory.</param>
    /// <param name="directoryPath">Path to the directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Completed indexing job.</returns>
    Task<IndexingJob> IndexDirectoryAsync(
        Guid directoryId,
        string directoryPath,
        CancellationToken cancellationToken);
}
