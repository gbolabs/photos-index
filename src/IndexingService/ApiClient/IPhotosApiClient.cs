using Shared.Dtos;
using Shared.Requests;
using Shared.Responses;

namespace IndexingService.ApiClient;

/// <summary>
/// Client for communicating with the Photos Index API.
/// </summary>
public interface IPhotosApiClient
{
    /// <summary>
    /// Gets all scan directories from the API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of scan directories.</returns>
    Task<IReadOnlyList<ScanDirectoryDto>> GetScanDirectoriesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets enabled scan directories from the API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of enabled scan directories.</returns>
    Task<IReadOnlyList<ScanDirectoryDto>> GetEnabledScanDirectoriesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Batch ingests files to the API.
    /// </summary>
    /// <param name="request">Batch ingest request containing files to ingest.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Batch operation response.</returns>
    Task<BatchOperationResponse> BatchIngestFilesAsync(
        BatchIngestFilesRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates the last scanned timestamp for a directory.
    /// </summary>
    /// <param name="directoryId">ID of the scan directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the operation.</returns>
    Task UpdateLastScannedAsync(Guid directoryId, CancellationToken cancellationToken);
}
