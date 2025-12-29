using Shared.Dtos;
using Shared.Requests;
using Shared.Responses;

namespace IndexingService.ApiClient;

/// <summary>
/// Request for ingesting a file with content.
/// </summary>
public record FileIngestRequest
{
    public Guid ScanDirectoryId { get; init; }
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required string FileHash { get; init; }
    public long FileSize { get; init; }
    public DateTime ModifiedAt { get; init; }
}

/// <summary>
/// Result of file ingestion.
/// </summary>
public record FileIngestResult
{
    public Guid IndexedFileId { get; init; }
    public required string FilePath { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsNewFile { get; init; }
}

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
    /// Ingests a single file with its content for distributed processing.
    /// </summary>
    /// <param name="request">The file ingest request with metadata.</param>
    /// <param name="fileContent">The file content stream.</param>
    /// <param name="contentType">The content type of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>File ingest result.</returns>
    Task<FileIngestResult> IngestFileWithContentAsync(
        FileIngestRequest request,
        Stream fileContent,
        string contentType,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates the last scanned timestamp for a directory.
    /// </summary>
    /// <param name="directoryId">ID of the scan directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the operation.</returns>
    Task UpdateLastScannedAsync(Guid directoryId, CancellationToken cancellationToken);

    /// <summary>
    /// Checks which files need to be reindexed based on their modification times.
    /// </summary>
    /// <param name="request">Request containing file paths and modification times.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of files with their reindex status.</returns>
    Task<IReadOnlyList<FileNeedsReindexDto>> CheckFilesNeedReindexAsync(
        CheckFilesNeedReindexRequest request,
        CancellationToken cancellationToken);
}
