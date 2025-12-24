using Minio;
using Minio.DataModel.Args;

namespace Shared.Storage;

/// <summary>
/// MinIO implementation of object storage.
/// Provides access to MinIO-compatible object storage systems.
/// </summary>
public class MinioObjectStorage : IObjectStorage
{
    private readonly IMinioClient _minioClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="MinioObjectStorage"/> class.
    /// </summary>
    /// <param name="minioClient">The MinIO client instance.</param>
    public MinioObjectStorage(IMinioClient minioClient)
    {
        _minioClient = minioClient ?? throw new ArgumentNullException(nameof(minioClient));
    }

    /// <inheritdoc />
    public async Task UploadAsync(
        string bucket,
        string key,
        Stream data,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var putObjectArgs = new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(key)
            .WithStreamData(data)
            .WithObjectSize(data.Length)
            .WithContentType(contentType);

        await _minioClient.PutObjectAsync(putObjectArgs, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Stream> DownloadAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var memoryStream = new MemoryStream();

        var getObjectArgs = new GetObjectArgs()
            .WithBucket(bucket)
            .WithObject(key)
            .WithCallbackStream(stream =>
            {
                stream.CopyTo(memoryStream);
                memoryStream.Position = 0;
            });

        await _minioClient.GetObjectAsync(getObjectArgs, cancellationToken)
            .ConfigureAwait(false);

        return memoryStream;
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            var statObjectArgs = new StatObjectArgs()
                .WithBucket(bucket)
                .WithObject(key);

            await _minioClient.StatObjectAsync(statObjectArgs, cancellationToken)
                .ConfigureAwait(false);

            return true;
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var removeObjectArgs = new RemoveObjectArgs()
            .WithBucket(bucket)
            .WithObject(key);

        await _minioClient.RemoveObjectAsync(removeObjectArgs, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task EnsureBucketExistsAsync(
        string bucket,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);

        var bucketExistsArgs = new BucketExistsArgs()
            .WithBucket(bucket);

        var exists = await _minioClient.BucketExistsAsync(bucketExistsArgs, cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
        {
            var makeBucketArgs = new MakeBucketArgs()
                .WithBucket(bucket);

            await _minioClient.MakeBucketAsync(makeBucketArgs, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
