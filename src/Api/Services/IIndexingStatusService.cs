using Shared.Dtos;

namespace Api.Services;

/// <summary>
/// Service for managing indexing status and progress.
/// </summary>
public interface IIndexingStatusService
{
    /// <summary>
    /// Gets the current indexing status.
    /// </summary>
    IndexingStatusDto GetStatus();

    /// <summary>
    /// Starts indexing for a directory.
    /// </summary>
    /// <param name="directoryId">The directory ID being indexed.</param>
    /// <param name="directoryPath">The directory path being indexed.</param>
    void StartIndexing(Guid directoryId, string directoryPath);

    /// <summary>
    /// Updates the indexing progress counters.
    /// </summary>
    /// <param name="scanned">Number of files scanned.</param>
    /// <param name="ingested">Number of files ingested.</param>
    /// <param name="failed">Number of files that failed.</param>
    void UpdateProgress(int scanned, int ingested, int failed);

    /// <summary>
    /// Stops the current indexing operation.
    /// </summary>
    void StopIndexing();
}
