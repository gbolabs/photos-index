using Database;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.Messages;

namespace Api.Consumers;

public class ThumbnailGeneratedConsumer : IConsumer<ThumbnailGeneratedMessage>
{
    private readonly PhotosDbContext _dbContext;
    private readonly ILogger<ThumbnailGeneratedConsumer> _logger;

    public ThumbnailGeneratedConsumer(
        PhotosDbContext dbContext,
        ILogger<ThumbnailGeneratedConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ThumbnailGeneratedMessage> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Received thumbnail for file {FileId}, CorrelationId: {CorrelationId}",
            message.IndexedFileId,
            message.CorrelationId);

        var file = await _dbContext.IndexedFiles
            .FirstOrDefaultAsync(f => f.Id == message.IndexedFileId, context.CancellationToken);

        if (file is null)
        {
            _logger.LogWarning("File {FileId} not found for thumbnail update", message.IndexedFileId);
            return;
        }

        // Always set the processing timestamp
        file.ThumbnailProcessedAt = DateTime.UtcNow;

        if (!message.Success)
        {
            file.LastError = message.ErrorMessage;
            file.RetryCount++;
            _logger.LogWarning(
                "Thumbnail generation failed for file {FileId}: {Error}",
                message.IndexedFileId,
                message.ErrorMessage);
        }
        else
        {
            file.ThumbnailPath = message.ThumbnailObjectKey;
            file.LastError = null;

            _logger.LogInformation(
                "Updated thumbnail path for file {FileId}: {ThumbnailPath}",
                message.IndexedFileId,
                message.ThumbnailObjectKey);
        }

        await _dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
