using Database;
using Database.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.Messages;
using Shared.Storage;

namespace Api.Services;

public class FileIngestService : IFileIngestService
{
    private readonly PhotosDbContext _dbContext;
    private readonly IObjectStorage _objectStorage;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<FileIngestService> _logger;
    private readonly IConfiguration _configuration;

    private const string TempBucket = "temp-images";
    private const string ImagesBucket = "images";

    public FileIngestService(
        PhotosDbContext dbContext,
        IObjectStorage objectStorage,
        IPublishEndpoint publishEndpoint,
        ILogger<FileIngestService> logger,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _objectStorage = objectStorage;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<FileIngestResult> IngestFileAsync(FileIngestRequest request, CancellationToken ct)
    {
        try
        {
            var existingFile = await _dbContext.IndexedFiles
                .FirstOrDefaultAsync(f => f.FilePath == request.FilePath, ct);

            var isNewFile = existingFile is null;
            IndexedFile file;

            if (isNewFile)
            {
                file = new IndexedFile
                {
                    Id = Guid.NewGuid(),
                    FilePath = request.FilePath,
                    FileName = request.FileName,
                    FileHash = request.FileHash,
                    FileSize = request.FileSize,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = request.ModifiedAt,
                    IndexedAt = DateTime.UtcNow,
                    IsDuplicate = false
                };

                // Check if file path matches any hidden folder rules
                var matchingHiddenFolder = await _dbContext.HiddenFolders
                    .FirstOrDefaultAsync(h =>
                        request.FilePath.StartsWith(h.FolderPath + "/") ||
                        request.FilePath.StartsWith(h.FolderPath + "\\"), ct);

                if (matchingHiddenFolder is not null)
                {
                    file.IsHidden = true;
                    file.HiddenCategory = HiddenCategory.FolderRule;
                    file.HiddenAt = DateTime.UtcNow;
                    file.HiddenByFolderId = matchingHiddenFolder.Id;
                    _logger.LogDebug("Auto-hiding file {FilePath} due to hidden folder rule {FolderId}",
                        request.FilePath, matchingHiddenFolder.Id);
                }

                _dbContext.IndexedFiles.Add(file);
            }
            else
            {
                file = existingFile!;
                file.FileHash = request.FileHash;
                file.FileSize = request.FileSize;
                file.ModifiedAt = request.ModifiedAt;
                file.IndexedAt = DateTime.UtcNow;
                file.LastError = null;
                file.RetryCount = 0;
            }

            await _dbContext.SaveChangesAsync(ct);

            if (request.FileContent is not null)
            {
                var bucket = _configuration["Minio:ImagesBucket"] ?? ImagesBucket;
                var metadataKey = $"metadata/{request.FileHash}";
                var thumbnailKey = $"thumbnail/{request.FileHash}";
                var contentType = request.ContentType ?? "application/octet-stream";

                await _objectStorage.EnsureBucketExistsAsync(bucket, ct);

                // Upload two copies - one for each processing service
                // Each service will delete its own copy after processing
                using var metadataStream = new MemoryStream();
                using var thumbnailStream = new MemoryStream();

                request.FileContent.Position = 0;
                await request.FileContent.CopyToAsync(metadataStream, ct);
                request.FileContent.Position = 0;
                await request.FileContent.CopyToAsync(thumbnailStream, ct);

                metadataStream.Position = 0;
                thumbnailStream.Position = 0;

                await Task.WhenAll(
                    _objectStorage.UploadAsync(bucket, metadataKey, metadataStream, contentType, ct),
                    _objectStorage.UploadAsync(bucket, thumbnailKey, thumbnailStream, contentType, ct)
                );

                _logger.LogInformation(
                    "Uploaded file {FilePath} to {Bucket} with keys {MetadataKey} and {ThumbnailKey}",
                    request.FilePath,
                    bucket,
                    metadataKey,
                    thumbnailKey);

                var message = new FileDiscoveredMessage
                {
                    CorrelationId = Guid.NewGuid(),
                    IndexedFileId = file.Id,
                    ScanDirectoryId = request.ScanDirectoryId,
                    FilePath = request.FilePath,
                    FileHash = request.FileHash,
                    FileSize = request.FileSize,
                    MetadataObjectKey = metadataKey,
                    ThumbnailObjectKey = thumbnailKey
                };

                await _publishEndpoint.Publish(message, ct);

                _logger.LogInformation(
                    "Published FileDiscoveredMessage for file {FileId}, CorrelationId: {CorrelationId}",
                    file.Id,
                    message.CorrelationId);
            }

            return new FileIngestResult
            {
                IndexedFileId = file.Id,
                FilePath = request.FilePath,
                Success = true,
                IsNewFile = isNewFile
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest file {FilePath}", request.FilePath);
            return new FileIngestResult
            {
                IndexedFileId = Guid.Empty,
                FilePath = request.FilePath,
                Success = false,
                ErrorMessage = ex.Message,
                IsNewFile = false
            };
        }
    }

    public async Task<IReadOnlyList<FileIngestResult>> IngestFilesAsync(
        IReadOnlyList<FileIngestRequest> requests,
        CancellationToken ct)
    {
        var results = new List<FileIngestResult>(requests.Count);

        foreach (var request in requests)
        {
            var result = await IngestFileAsync(request, ct);
            results.Add(result);
        }

        return results;
    }
}
