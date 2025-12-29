using System.Collections.Concurrent;
using System.Diagnostics;
using IndexingService.ApiClient;
using IndexingService.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Dtos;
using Shared.Requests;

namespace IndexingService.Services;

public class IndexingOrchestrator : IIndexingOrchestrator
{
    private readonly IPhotosApiClient _apiClient;
    private readonly IFileScanner _fileScanner;
    private readonly IHashComputer _hashComputer;
    private readonly IMetadataExtractor _metadataExtractor;
    private readonly IScanSessionService _scanSession;
    private readonly IIndexerStatusService _statusService;
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
        IScanSessionService scanSession,
        IIndexerStatusService statusService,
        ILogger<IndexingOrchestrator> logger,
        IOptions<IndexingOptions> options)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _fileScanner = fileScanner ?? throw new ArgumentNullException(nameof(fileScanner));
        _hashComputer = hashComputer ?? throw new ArgumentNullException(nameof(hashComputer));
        _metadataExtractor = metadataExtractor ?? throw new ArgumentNullException(nameof(metadataExtractor));
        _scanSession = scanSession ?? throw new ArgumentNullException(nameof(scanSession));
        _statusService = statusService ?? throw new ArgumentNullException(nameof(statusService));
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
            // Start a new scan session for this cycle
            _scanSession.StartNewSession();
            _logger.LogInformation("Starting indexing cycle (Session: {SessionId})", _scanSession.SessionId);

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
            // Check if this directory or a parent has already been scanned in this session
            if (_scanSession.IsPathCoveredByScannedDirectory(directoryPath))
            {
                _logger.LogInformation(
                    "Skipping directory {Path} - already covered by scanned parent in session {SessionId}",
                    directoryPath, _scanSession.SessionId);
                return job.Complete(0, 0, 0, 0);
            }

            _logger.LogInformation("Starting progressive indexing for directory {Path} (batch size: {BatchSize}, session: {SessionId})",
                directoryPath, _options.BatchSize, _scanSession.SessionId);

            if (!Directory.Exists(directoryPath))
            {
                _logger.LogError("Directory does not exist: {Path}", directoryPath);
                return job.Fail($"Directory does not exist: {directoryPath}");
            }

            // Update status with current directory
            _statusService.SetCurrentDirectory(directoryPath);
            _statusService.SetActivity($"Scanning: {Path.GetFileName(directoryPath)}");

            var totalScanned = 0;
            var totalSkipped = 0;
            var totalProcessed = 0;
            var totalIngested = 0;
            var totalFailed = 0;
            long totalBytesScanned = 0;
            long totalBytesProcessed = 0;
            var currentBatch = new List<ScannedFile>(_options.BatchSize);

            // Progressive scan and ingest - process files in batches as we scan
            await foreach (var file in _fileScanner.ScanAsync(directoryPath, includeSubdirectories: true, cancellationToken))
            {
                currentBatch.Add(file);
                totalScanned++;
                totalBytesScanned += file.FileSizeBytes;

                // Update progress as we scan
                _statusService.SetProgress(totalIngested + totalFailed, totalScanned);

                // Process batch when full
                if (currentBatch.Count >= _options.BatchSize)
                {
                    _statusService.SetActivity($"Processing batch ({totalIngested} ingested)");

                    var batchBytes = currentBatch.Sum(f => f.FileSizeBytes);
                    var (processed, ingested, failed, skipped) = await ProcessAndIngestBatchAsync(
                        directoryId, currentBatch, cancellationToken);

                    totalProcessed += processed;
                    totalIngested += ingested;
                    totalFailed += failed;
                    totalSkipped += skipped;
                    totalBytesProcessed += batchBytes;

                    // Update progress after batch completes
                    _statusService.SetProgress(totalIngested + totalFailed, totalScanned);
                    _statusService.SetBytesProgress(totalBytesProcessed, totalBytesScanned);

                    _logger.LogInformation(
                        "Progress: {Scanned} scanned, {Skipped} unchanged, {Ingested} ingested so far in {Path}",
                        totalScanned, totalSkipped, totalIngested, directoryPath);

                    currentBatch.Clear();
                }
            }

            // Process remaining files in last batch
            if (currentBatch.Count > 0)
            {
                _statusService.SetActivity($"Processing final batch ({currentBatch.Count} files)");

                var batchBytes = currentBatch.Sum(f => f.FileSizeBytes);
                var (processed, ingested, failed, skipped) = await ProcessAndIngestBatchAsync(
                    directoryId, currentBatch, cancellationToken);

                totalProcessed += processed;
                totalIngested += ingested;
                totalFailed += failed;
                totalSkipped += skipped;
                totalBytesProcessed += batchBytes;

                // Final progress update
                _statusService.SetProgress(totalIngested + totalFailed, totalScanned);
                _statusService.SetBytesProgress(totalBytesProcessed, totalBytesScanned);
            }

            await _apiClient.UpdateLastScannedAsync(directoryId, cancellationToken);

            // Mark this directory as fully scanned in the current session
            _scanSession.MarkDirectoryScanned(directoryPath);

            _logger.LogInformation(
                "Completed indexing for directory {Path}: {Scanned} scanned, {Skipped} unchanged, {Processed} processed, {Ingested} ingested, {Failed} failed (session: {SessionId})",
                directoryPath, totalScanned, totalSkipped, totalProcessed, totalIngested, totalFailed, _scanSession.SessionId);

            activity?.SetTag("files.scanned", totalScanned);
            activity?.SetTag("files.skipped", totalSkipped);
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

    private async Task<(int processed, int ingested, int failed, int skipped)> ProcessAndIngestBatchAsync(
        Guid directoryId, List<ScannedFile> batch, CancellationToken cancellationToken)
    {
        // Filter out files already processed in this session
        var sessionFilteredBatch = batch.Where(f => !_scanSession.IsFileProcessed(f.FullPath)).ToList();
        var sessionSkipped = batch.Count - sessionFilteredBatch.Count;

        if (sessionSkipped > 0)
        {
            _logger.LogDebug("Skipping {Count} files already processed in session {SessionId}",
                sessionSkipped, _scanSession.SessionId);
        }

        if (sessionFilteredBatch.Count == 0)
        {
            return (0, 0, 0, batch.Count);
        }

        // First, check which files need reindexing (skip unchanged files)
        var checkRequest = new CheckFilesNeedReindexRequest
        {
            DirectoryId = directoryId,
            Files = sessionFilteredBatch.Select(f => new FileModificationInfo
            {
                FilePath = f.FullPath,
                ModifiedAt = f.LastModifiedUtc
            }).ToList()
        };

        IReadOnlyList<FileNeedsReindexDto> reindexStatus;
        try
        {
            reindexStatus = await _apiClient.CheckFilesNeedReindexAsync(checkRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            // If the API call fails, fall back to processing all files
            _logger.LogWarning(ex, "Failed to check reindex status, processing all files in batch");
            reindexStatus = sessionFilteredBatch.Select(f => new FileNeedsReindexDto
            {
                FilePath = f.FullPath,
                LastModifiedAt = f.LastModifiedUtc,
                NeedsReindex = true
            }).ToList();
        }

        var filesToReindex = new HashSet<string>(
            reindexStatus.Where(r => r.NeedsReindex).Select(r => r.FilePath));

        var skippedCount = sessionSkipped + (sessionFilteredBatch.Count - filesToReindex.Count);

        if (skippedCount > 0)
        {
            _logger.LogDebug("Skipping {Count} unchanged files in batch", skippedCount);
        }

        // Filter batch to only include files that need reindexing
        var filteredBatch = sessionFilteredBatch.Where(f => filesToReindex.Contains(f.FullPath)).ToList();

        if (filteredBatch.Count == 0)
        {
            _logger.LogDebug("All files in batch are unchanged, skipping processing");
            return (0, 0, 0, skippedCount);
        }

        var filePaths = filteredBatch.Select(f => f.FullPath).ToList();

        // Compute hashes for batch (already parallel)
        var hashResults = new ConcurrentDictionary<string, HashResult>();
        await foreach (var hashResult in _hashComputer.ComputeBatchAsync(filePaths, _options.MaxParallelism, cancellationToken))
        {
            hashResults[hashResult.FilePath] = hashResult;
        }

        var filesToProcess = filteredBatch
            .Where(f => hashResults.TryGetValue(f.FullPath, out var hr) && hr.Success)
            .ToList();

        // In distributed mode, upload file content for each file
        if (_isDistributedMode)
        {
            var (processed, ingested, failed) = await ProcessAndIngestDistributedAsync(directoryId, filesToProcess, hashResults, cancellationToken);
            return (processed, ingested, failed, skippedCount);
        }

        // Local processing mode: extract metadata/thumbnails locally
        {
            var (processed, ingested, failed) = await ProcessAndIngestLocalAsync(directoryId, filesToProcess, hashResults, cancellationToken);
            return (processed, ingested, failed, skippedCount);
        }
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
                    _scanSession.MarkFileProcessed(scannedFile.FullPath);
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

        _logger.LogInformation("Distributed upload completed: {Ingested} succeeded, {Failed} failed (session files: {SessionFiles})",
            ingested, failed, _scanSession.ProcessedFileCount);
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

            // Mark successfully processed files in session
            foreach (var file in processedFiles)
            {
                _scanSession.MarkFileProcessed(file.ScannedFile.FullPath);
            }

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
