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

            string? objectKey = null;

            if (request.FileContent is not null)
            {
                var bucket = _configuration["Minio:ImagesBucket"] ?? ImagesBucket;
                objectKey = $"files/{request.FileHash}";

                await _objectStorage.EnsureBucketExistsAsync(bucket, ct);
                await _objectStorage.UploadAsync(
                    bucket,
                    objectKey,
                    request.FileContent,
                    request.ContentType ?? "application/octet-stream",
                    ct);

                _logger.LogInformation(
                    "Uploaded file {FilePath} to {Bucket}/{ObjectKey}",
                    request.FilePath,
                    bucket,
                    objectKey);

                var scanDirectory = await _dbContext.ScanDirectories
                    .FirstOrDefaultAsync(d => d.Id == request.ScanDirectoryId, ct);

                var message = new FileDiscoveredMessage
                {
                    CorrelationId = Guid.NewGuid(),
                    IndexedFileId = file.Id,
                    ScanDirectoryId = request.ScanDirectoryId,
                    FilePath = request.FilePath,
                    FileHash = request.FileHash,
                    FileSize = request.FileSize,
                    ObjectKey = objectKey
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
