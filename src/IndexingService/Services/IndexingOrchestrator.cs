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

    private static readonly ActivitySource ActivitySource = new("PhotosIndex.IndexingService.Orchestrator");

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

        _logger.LogInformation("IndexingOrchestrator configured: GenerateThumbnails={GenerateThumbnails}, BatchSize={BatchSize}, MaxParallelism={MaxParallelism}",
            _options.GenerateThumbnails, _options.BatchSize, _options.MaxParallelism);
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
        var processedFiles = new List<ProcessedFile>();
        var filePaths = batch.Select(f => f.FullPath).ToList();

        // Compute hashes for batch
        var hashResults = new Dictionary<string, HashResult>();
        await foreach (var hashResult in _hashComputer.ComputeBatchAsync(filePaths, _options.MaxParallelism, cancellationToken))
        {
            hashResults[hashResult.FilePath] = hashResult;
        }

        // Extract metadata and optionally generate thumbnails
        foreach (var scannedFile in batch)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (!hashResults.TryGetValue(scannedFile.FullPath, out var hashResult) || !hashResult.Success)
                continue;

            try
            {
                var metadata = await _metadataExtractor.ExtractAsync(scannedFile.FullPath, cancellationToken);

                byte[]? thumbnail = null;
                if (_options.GenerateThumbnails)
                {
                    try
                    {
                        thumbnail = await _metadataExtractor.GenerateThumbnailAsync(
                            scannedFile.FullPath,
                            new ThumbnailOptions { MaxWidth = 200, MaxHeight = 200, Quality = 80 },
                            cancellationToken);
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
        }

        if (processedFiles.Count == 0)
            return (0, 0, batch.Count);

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
                ThumbnailBase64 = f.Thumbnail != null ? Convert.ToBase64String(f.Thumbnail) : null
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
