namespace Shared.Storage;

/// <summary>
/// Interface for object storage operations.
/// Abstracts the underlying storage implementation (MinIO, S3, etc.).
/// </summary>
public interface IObjectStorage
{
    /// <summary>
    /// Uploads data to object storage.
    /// </summary>
    /// <param name="bucket">The bucket name.</param>
    /// <param name="key">The object key (path within the bucket).</param>
    /// <param name="data">The stream containing the data to upload.</param>
    /// <param name="contentType">The MIME type of the content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UploadAsync(
        string bucket,
        string key,
        Stream data,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads data from object storage.
    /// </summary>
    /// <param name="bucket">The bucket name.</param>
    /// <param name="key">The object key (path within the bucket).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A stream containing the downloaded data. Caller is responsible for disposing the stream.</returns>
    Task<Stream> DownloadAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an object exists in storage.
    /// </summary>
    /// <param name="bucket">The bucket name.</param>
    /// <param name="key">The object key (path within the bucket).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the object exists, false otherwise.</returns>
    Task<bool> ExistsAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an object from storage.
    /// </summary>
    /// <param name="bucket">The bucket name.</param>
    /// <param name="key">The object key (path within the bucket).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures that a bucket exists, creating it if necessary.
    /// </summary>
    /// <param name="bucket">The bucket name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EnsureBucketExistsAsync(
        string bucket,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists objects in a bucket with an optional prefix filter.
    /// </summary>
    /// <param name="bucket">The bucket name.</param>
    /// <param name="prefix">Optional prefix to filter objects.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of storage objects.</returns>
    Task<IReadOnlyList<StorageObject>> ListObjectsAsync(
        string bucket,
        string? prefix = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an object in storage.
/// </summary>
public record StorageObject
{
    public required string Key { get; init; }
    public long Size { get; init; }
    public DateTime LastModified { get; init; }
    public string? ContentType { get; init; }
}
