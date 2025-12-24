using MassTransit;
using Shared.Messages;
using Shared.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace ThumbnailService;

public class FileDiscoveredConsumer : IConsumer<FileDiscoveredMessage>
{
    private readonly IObjectStorage _objectStorage;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<FileDiscoveredConsumer> _logger;
    private readonly IConfiguration _configuration;

    private const int ThumbnailMaxSize = 300;

    public FileDiscoveredConsumer(
        IObjectStorage objectStorage,
        IPublishEndpoint publishEndpoint,
        ILogger<FileDiscoveredConsumer> logger,
        IConfiguration configuration)
    {
        _objectStorage = objectStorage;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task Consume(ConsumeContext<FileDiscoveredMessage> context)
    {
        var message = context.Message;
        var ct = context.CancellationToken;

        _logger.LogInformation(
            "Generating thumbnail for file {FileId}, CorrelationId: {CorrelationId}",
            message.IndexedFileId,
            message.CorrelationId);

        try
        {
            var imagesBucket = _configuration["Minio:ImagesBucket"] ?? "images";
            var thumbnailsBucket = _configuration["Minio:ThumbnailsBucket"] ?? "thumbnails";

            await using var imageStream = await _objectStorage.DownloadAsync(imagesBucket, message.ObjectKey, ct);

            using var image = await Image.LoadAsync(imageStream, ct);

            // Calculate thumbnail dimensions maintaining aspect ratio
            var (width, height) = CalculateThumbnailSize(image.Width, image.Height);

            // Resize the image
            image.Mutate(x => x.Resize(width, height));

            // Save to memory stream
            using var thumbnailStream = new MemoryStream();
            await image.SaveAsync(thumbnailStream, new JpegEncoder { Quality = 85 }, ct);
            thumbnailStream.Position = 0;

            // Upload thumbnail to MinIO
            var thumbnailKey = $"thumbs/{message.FileHash}.jpg";
            await _objectStorage.EnsureBucketExistsAsync(thumbnailsBucket, ct);
            await _objectStorage.UploadAsync(
                thumbnailsBucket,
                thumbnailKey,
                thumbnailStream,
                "image/jpeg",
                ct);

            var result = new ThumbnailGeneratedMessage
            {
                CorrelationId = message.CorrelationId,
                IndexedFileId = message.IndexedFileId,
                Success = true,
                ThumbnailObjectKey = thumbnailKey
            };

            await _publishEndpoint.Publish(result, ct);

            _logger.LogInformation(
                "Published ThumbnailGeneratedMessage for file {FileId}: {ThumbnailKey}",
                message.IndexedFileId,
                thumbnailKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate thumbnail for file {FileId}", message.IndexedFileId);

            var errorResult = new ThumbnailGeneratedMessage
            {
                CorrelationId = message.CorrelationId,
                IndexedFileId = message.IndexedFileId,
                Success = false,
                ErrorMessage = ex.Message
            };

            await _publishEndpoint.Publish(errorResult, ct);
        }
    }

    private static (int width, int height) CalculateThumbnailSize(int originalWidth, int originalHeight)
    {
        if (originalWidth <= ThumbnailMaxSize && originalHeight <= ThumbnailMaxSize)
        {
            return (originalWidth, originalHeight);
        }

        double ratio;
        if (originalWidth > originalHeight)
        {
            ratio = (double)ThumbnailMaxSize / originalWidth;
        }
        else
        {
            ratio = (double)ThumbnailMaxSize / originalHeight;
        }

        return ((int)(originalWidth * ratio), (int)(originalHeight * ratio));
    }
}
