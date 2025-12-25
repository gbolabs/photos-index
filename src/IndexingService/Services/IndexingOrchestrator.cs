using System.Collections.Concurrent;
using System.Diagnostics;
using IndexingService.ApiClient;
using IndexingService.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Requests;

namespace IndexingService.Services;

public class IndexingOrchestrator : IIndexingOrchestrator
{
    private readonly IPhotosApiClient _apiClient;
    private readonly IFileScanner _fileScanner;
    private readonly IHashComputer _hashComputer;
    private readonly IMetadataExtractor _metadataExtractor;
    private readonly ILogger<IndexingOrchestrator> _logger;
    private readonly IndexingOptions _options;
    private readonly bool _isDistributedMode;

    private static readonly ActivitySource ActivitySource = new("PhotosIndex.IndexingService.Orchestrator");

    // Content type mapping for image files
    private static readonly Dictionary<string, string> ContentTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".png", "image/png" },
        { ".gif", "image/gif" },
        { ".heic", "image/heic" },
        { ".webp", "image/webp" },
        { ".bmp", "image/bmp" },
        { ".tiff", "image/tiff" },
        { ".tif", "image/tiff" }
    };

    public IndexingOrchestrator(
        IPhotosApiClient apiClient,
        IFileScanner fileScanner,
        IHashComputer hashComputer,
        IMetadataExtractor metadataExtractor,
        ILogger<IndexingOrchestrator> logger,
        IOptions<IndexingOptions> options)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _fileScanner = fileScanner ?? throw new ArgumentNullException(nameof(fileScanner));
        _hashComputer = hashComputer ?? throw new ArgumentNullException(nameof(hashComputer));
        _metadataExtractor = metadataExtractor ?? throw new ArgumentNullException(nameof(metadataExtractor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new IndexingOptions();

        // Distributed mode: both local processing disabled, rely on remote services
        _isDistributedMode = !_options.ExtractMetadata && !_options.GenerateThumbnails;

        _logger.LogInformation("IndexingOrchestrator configured: ExtractMetadata={ExtractMetadata}, GenerateThumbnails={GenerateThumbnails}, BatchSize={BatchSize}, MaxParallelism={MaxParallelism}, DistributedMode={DistributedMode}",
            _options.ExtractMetadata, _options.GenerateThumbnails, _options.BatchSize, _options.MaxParallelism, _isDistributedMode);
    }

    public async Task<IReadOnlyList<IndexingJob>> RunIndexingCycleAsync(CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("RunIndexingCycle");

        try
        {
            _logger.LogInformation("Starting indexing cycle");

            var directories = await _apiClient.GetEnabledScanDirectoriesAsync(cancellationToken);

            if (directories.Count == 0)
            {
                _logger.LogWarning("No enabled scan directories found");
                return Array.Empty<IndexingJob>();
            }

            _logger.LogInformation("Found {Count} enabled scan directories", directories.Count);
            activity?.SetTag("directories.count", directories.Count);

            var jobs = new List<IndexingJob>();
            foreach (var directory in directories)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Indexing cycle cancelled");
                    break;
                }

                try
                {
                    var job = await IndexDirectoryAsync(directory.Id, directory.Path, cancellationToken);
                    jobs.Add(job);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to index directory {Path}", directory.Path);
                    jobs.Add(IndexingJob.Start(directory.Id, directory.Path).Fail($"Indexing failed: {ex.Message}"));
                }
            }

            var successCount = jobs.Count(j => j.Success);
            var failureCount = jobs.Count(j => !j.Success);

            _logger.LogInformation("Indexing cycle completed: {Success} succeeded, {Failed} failed", successCount, failureCount);

            activity?.SetTag("jobs.success", successCount);
            activity?.SetTag("jobs.failure", failureCount);

            return jobs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Indexing cycle failed");
            activity?.SetTag("error", true);
            throw;
        }
    }

    public async Task<IndexingJob> IndexDirectoryAsync(Guid directoryId, string directoryPath, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("IndexDirectory");
        activity?.SetTag("directory.id", directoryId);
        activity?.SetTag("directory.path", directoryPath);

        var job = IndexingJob.Start(directoryId, directoryPath);

        try
        {
            _logger.LogInformation("Starting progressive indexing for directory {Path} (batch size: {BatchSize})",
                directoryPath, _options.BatchSize);

            if (!Directory.Exists(directoryPath))
            {
                _logger.LogError("Directory does not exist: {Path}", directoryPath);
                return job.Fail($"Directory does not exist: {directoryPath}");
            }

            var totalScanned = 0;
            var totalProcessed = 0;
            var totalIngested = 0;
            var totalFailed = 0;
            var currentBatch = new List<ScannedFile>(_options.BatchSize);

            // Progressive scan and ingest - process files in batches as we scan
            await foreach (var file in _fileScanner.ScanAsync(directoryPath, includeSubdirectories: true, cancellationToken))
            {
                currentBatch.Add(file);
                totalScanned++;

                // Process batch when full
                if (currentBatch.Count >= _options.BatchSize)
                {
                    var (processed, ingested, failed) = await ProcessAndIngestBatchAsync(
                        directoryId, currentBatch, cancellationToken);

                    totalProcessed += processed;
                    totalIngested += ingested;
                    totalFailed += failed;

                    _logger.LogInformation(
                        "Progress: {Scanned} scanned, {Ingested} ingested so far in {Path}",
                        totalScanned, totalIngested, directoryPath);

                    currentBatch.Clear();
                }
            }

            // Process remaining files in last batch
            if (currentBatch.Count > 0)
            {
                var (processed, ingested, failed) = await ProcessAndIngestBatchAsync(
                    directoryId, currentBatch, cancellationToken);

                totalProcessed += processed;
                totalIngested += ingested;
                totalFailed += failed;
            }

            await _apiClient.UpdateLastScannedAsync(directoryId, cancellationToken);

            _logger.LogInformation(
                "Completed indexing for directory {Path}: {Scanned} scanned, {Processed} processed, {Ingested} ingested, {Failed} failed",
                directoryPath, totalScanned, totalProcessed, totalIngested, totalFailed);

            activity?.SetTag("files.scanned", totalScanned);
            activity?.SetTag("files.processed", totalProcessed);
            activity?.SetTag("files.ingested", totalIngested);
            activity?.SetTag("files.failed", totalFailed);

            return job.Complete(totalScanned, totalProcessed, totalIngested, totalFailed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index directory {Path}", directoryPath);
            activity?.SetTag("error", true);
            return job.Fail($"Indexing failed: {ex.Message}");
        }
    }

    private async Task<(int processed, int ingested, int failed)> ProcessAndIngestBatchAsync(
        Guid directoryId, List<ScannedFile> batch, CancellationToken cancellationToken)
    {
        var filePaths = batch.Select(f => f.FullPath).ToList();

        // Compute hashes for batch (already parallel)
        var hashResults = new ConcurrentDictionary<string, HashResult>();
        await foreach (var hashResult in _hashComputer.ComputeBatchAsync(filePaths, _options.MaxParallelism, cancellationToken))
        {
            hashResults[hashResult.FilePath] = hashResult;
        }

        var filesToProcess = batch
            .Where(f => hashResults.TryGetValue(f.FullPath, out var hr) && hr.Success)
            .ToList();

        // In distributed mode, upload file content for each file
        if (_isDistributedMode)
        {
            return await ProcessAndIngestDistributedAsync(directoryId, filesToProcess, hashResults, cancellationToken);
        }

        // Local processing mode: extract metadata/thumbnails locally
        return await ProcessAndIngestLocalAsync(directoryId, filesToProcess, hashResults, cancellationToken);
    }

    private async Task<(int processed, int ingested, int failed)> ProcessAndIngestDistributedAsync(
        Guid directoryId,
        List<ScannedFile> filesToProcess,
        ConcurrentDictionary<string, HashResult> hashResults,
        CancellationToken cancellationToken)
    {
        var ingested = 0;
        var failed = 0;

        _logger.LogInformation("Distributed mode: uploading {Count} files with content for remote processing", filesToProcess.Count);

        // Process files sequentially to avoid overwhelming the network with large uploads
        // Use semaphore for controlled parallelism
        using var semaphore = new SemaphoreSlim(_options.MaxParallelism);
        var tasks = filesToProcess.Select(async scannedFile =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var hashResult = hashResults[scannedFile.FullPath];
                var extension = Path.GetExtension(scannedFile.FullPath);
                var contentType = ContentTypeMap.TryGetValue(extension, out var ct) ? ct : "application/octet-stream";

                var request = new FileIngestRequest
                {
                    ScanDirectoryId = directoryId,
                    FilePath = scannedFile.FullPath,
                    FileName = scannedFile.FileName,
                    FileHash = hashResult.Hash,
                    FileSize = scannedFile.FileSizeBytes,
                    ModifiedAt = scannedFile.LastModifiedUtc
                };

                await using var fileStream = new FileStream(
                    scannedFile.FullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    useAsync: true);

                var result = await _apiClient.IngestFileWithContentAsync(request, fileStream, contentType, cancellationToken);

                if (result.Success)
                {
                    Interlocked.Increment(ref ingested);
                    _logger.LogDebug("Uploaded file {Path} for distributed processing", scannedFile.FullPath);
                }
                else
                {
                    Interlocked.Increment(ref failed);
                    _logger.LogWarning("Failed to upload file {Path}: {Error}", scannedFile.FullPath, result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failed);
                _logger.LogError(ex, "Error uploading file {Path}", scannedFile.FullPath);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        _logger.LogInformation("Distributed upload completed: {Ingested} succeeded, {Failed} failed", ingested, failed);
        return (filesToProcess.Count, ingested, failed);
    }

    private async Task<(int processed, int ingested, int failed)> ProcessAndIngestLocalAsync(
        Guid directoryId,
        List<ScannedFile> filesToProcess,
        ConcurrentDictionary<string, HashResult> hashResults,
        CancellationToken cancellationToken)
    {
        var processedFiles = new ConcurrentBag<ProcessedFile>();

        await Parallel.ForEachAsync(
            filesToProcess,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = _options.MaxParallelism,
                CancellationToken = cancellationToken
            },
            async (scannedFile, ct) =>
            {
                var hashResult = hashResults[scannedFile.FullPath];

                try
                {
                    // Extract metadata locally only if enabled (disabled for distributed processing)
                    var metadata = _options.ExtractMetadata
                        ? await _metadataExtractor.ExtractAsync(scannedFile.FullPath, ct)
                        : new ImageMetadata(); // Empty metadata - will be extracted by MetadataService

                    byte[]? thumbnail = null;
                    if (_options.GenerateThumbnails)
                    {
                        try
                        {
                            thumbnail = await _metadataExtractor.GenerateThumbnailAsync(
                                scannedFile.FullPath,
                                new ThumbnailOptions { MaxWidth = 200, MaxHeight = 200, Quality = 80 },
                                ct);
                        }
                        catch { /* Thumbnail failure is non-fatal */ }
                    }

                    processedFiles.Add(new ProcessedFile
                    {
                        ScannedFile = scannedFile,
                        Hash = hashResult.Hash,
                        Metadata = metadata,
                        Thumbnail = thumbnail
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process {Path}", scannedFile.FullPath);
                }
            });

        if (processedFiles.Count == 0)
            return (0, 0, filesToProcess.Count);

        // Ingest to API
        var request = new BatchIngestFilesRequest
        {
            ScanDirectoryId = directoryId,
            Files = processedFiles.Select(f => new IngestFileItem
            {
                FilePath = f.ScannedFile.FullPath,
                FileName = f.ScannedFile.FileName,
                FileHash = f.Hash,
                FileSize = f.ScannedFile.FileSizeBytes,
                Width = f.Metadata.Width,
                Height = f.Metadata.Height,
                CreatedAt = f.Metadata.DateTaken,
                ModifiedAt = f.ScannedFile.LastModifiedUtc,
                ThumbnailBase64 = f.Thumbnail != null ? Convert.ToBase64String(f.Thumbnail) : null,
                DateTaken = f.Metadata.DateTaken,
                CameraMake = f.Metadata.CameraMake,
                CameraModel = f.Metadata.CameraModel,
                GpsLatitude = f.Metadata.Latitude,
                GpsLongitude = f.Metadata.Longitude,
                Iso = f.Metadata.Iso,
                Aperture = f.Metadata.Aperture,
                ShutterSpeed = f.Metadata.ShutterSpeed
            }).ToList()
        };

        try
        {
            var response = await _apiClient.BatchIngestFilesAsync(request, cancellationToken);
            return (processedFiles.Count, response.Succeeded, response.Failed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest batch of {Count} files", processedFiles.Count);
            return (processedFiles.Count, 0, processedFiles.Count);
        }
    }

    private class ProcessedFile
    {
        public required ScannedFile ScannedFile { get; init; }
        public required string Hash { get; init; }
        public required ImageMetadata Metadata { get; init; }
        public byte[]? Thumbnail { get; init; }
    }
}
