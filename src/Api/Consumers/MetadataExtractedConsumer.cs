using Database;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.Messages;

namespace Api.Consumers;

public class MetadataExtractedConsumer : IConsumer<MetadataExtractedMessage>
{
    private readonly PhotosDbContext _dbContext;
    private readonly ILogger<MetadataExtractedConsumer> _logger;

    public MetadataExtractedConsumer(
        PhotosDbContext dbContext,
        ILogger<MetadataExtractedConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<MetadataExtractedMessage> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Received metadata for file {FileId}, CorrelationId: {CorrelationId}",
            message.IndexedFileId,
            message.CorrelationId);

        var file = await _dbContext.IndexedFiles
            .FirstOrDefaultAsync(f => f.Id == message.IndexedFileId, context.CancellationToken);

        if (file is null)
        {
            _logger.LogWarning("File {FileId} not found for metadata update", message.IndexedFileId);
            return;
        }

        // Always set the processing timestamp
        file.MetadataProcessedAt = DateTime.UtcNow;

        if (!message.Success)
        {
            file.LastError = message.ErrorMessage;
            file.RetryCount++;
            _logger.LogWarning(
                "Metadata extraction failed for file {FileId}: {Error}",
                message.IndexedFileId,
                message.ErrorMessage);
        }
        else
        {
            file.Width = message.Width;
            file.Height = message.Height;
            file.DateTaken = message.DateTaken;
            file.CameraMake = message.CameraMake;
            file.CameraModel = message.CameraModel;
            file.GpsLatitude = message.GpsLatitude;
            file.GpsLongitude = message.GpsLongitude;
            file.Iso = message.Iso;
            file.Aperture = message.Aperture;
            file.ShutterSpeed = message.ShutterSpeed;
            file.LastError = null;

            _logger.LogInformation(
                "Updated metadata for file {FileId}: {Width}x{Height}, DateTaken: {DateTaken}",
                message.IndexedFileId,
                message.Width,
                message.Height,
                message.DateTaken);
        }

        await _dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
