using System.Diagnostics;
using IndexingService.ApiClient;
using IndexingService.Models;
using Microsoft.Extensions.Logging;
using Shared.Requests;

namespace IndexingService.Services;

public class IndexingOrchestrator : IIndexingOrchestrator
{
    private readonly IPhotosApiClient _apiClient;
    private readonly IFileScanner _fileScanner;
    private readonly IHashComputer _hashComputer;
    private readonly IMetadataExtractor _metadataExtractor;
    private readonly ILogger<IndexingOrchestrator> _logger;

    private const int BatchSize = 100;
    private const int MaxParallelism = 4;

    private static readonly ActivitySource ActivitySource = new("PhotosIndex.IndexingService.Orchestrator");

    public IndexingOrchestrator(
        IPhotosApiClient apiClient,
        IFileScanner fileScanner,
        IHashComputer hashComputer,
        IMetadataExtractor metadataExtractor,
        ILogger<IndexingOrchestrator> logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _fileScanner = fileScanner ?? throw new ArgumentNullException(nameof(fileScanner));
        _hashComputer = hashComputer ?? throw new ArgumentNullException(nameof(hashComputer));
        _metadataExtractor = metadataExtractor ?? throw new ArgumentNullException(nameof(metadataExtractor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            _logger.LogInformation("Starting indexing for directory {Path}", directoryPath);

            if (!Directory.Exists(directoryPath))
            {
                _logger.LogError("Directory does not exist: {Path}", directoryPath);
                return job.Fail($"Directory does not exist: {directoryPath}");
            }

            var scannedFiles = new List<ScannedFile>();
            await foreach (var file in _fileScanner.ScanAsync(directoryPath, includeSubdirectories: true, cancellationToken))
            {
                scannedFiles.Add(file);
            }

            _logger.LogInformation("Scanned {Count} files in directory {Path}", scannedFiles.Count, directoryPath);

            activity?.SetTag("files.scanned", scannedFiles.Count);

            if (scannedFiles.Count == 0)
            {
                _logger.LogInformation("No files found in directory {Path}", directoryPath);
                await _apiClient.UpdateLastScannedAsync(directoryId, cancellationToken);
                return job.Complete(0, 0, 0, 0);
            }

            var processedFiles = new List<ProcessedFile>();
            var filePaths = scannedFiles.Select(f => f.FullPath).ToList();

            var hashResults = new Dictionary<string, HashResult>();
            await foreach (var hashResult in _hashComputer.ComputeBatchAsync(filePaths, MaxParallelism, cancellationToken))
            {
                hashResults[hashResult.FilePath] = hashResult;
            }

            _logger.LogInformation("Computed hashes for {Count} files in directory {Path}", hashResults.Count(h => h.Value.Success), directoryPath);

            foreach (var scannedFile in scannedFiles)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (!hashResults.TryGetValue(scannedFile.FullPath, out var hashResult) || !hashResult.Success)
                {
                    _logger.LogWarning("Skipping file {Path} - hash computation failed", scannedFile.FullPath);
                    continue;
                }

                try
                {
                    var metadata = await _metadataExtractor.ExtractAsync(scannedFile.FullPath, cancellationToken);

                    byte[]? thumbnail = null;
                    try
                    {
                        thumbnail = await _metadataExtractor.GenerateThumbnailAsync(
                            scannedFile.FullPath,
                            new ThumbnailOptions { MaxWidth = 200, MaxHeight = 200, Quality = 80 },
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to generate thumbnail for {Path}", scannedFile.FullPath);
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
                    _logger.LogWarning(ex, "Failed to extract metadata for {Path}", scannedFile.FullPath);
                }
            }

            _logger.LogInformation("Processed {Count} files in directory {Path}", processedFiles.Count, directoryPath);

            activity?.SetTag("files.processed", processedFiles.Count);

            if (processedFiles.Count == 0)
            {
                _logger.LogWarning("No files successfully processed in directory {Path}", directoryPath);
                await _apiClient.UpdateLastScannedAsync(directoryId, cancellationToken);
                return job.Complete(scannedFiles.Count, 0, 0, scannedFiles.Count);
            }

            var totalIngested = 0;
            var totalFailed = 0;

            foreach (var batch in processedFiles.Chunk(BatchSize))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var request = new BatchIngestFilesRequest
                {
                    ScanDirectoryId = directoryId,
                    Files = batch.Select(f => new IngestFileItem
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
                    totalIngested += response.Succeeded;
                    totalFailed += response.Failed;

                    _logger.LogInformation("Batch ingested: {Success} succeeded, {Failed} failed", response.Succeeded, response.Failed);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to ingest batch of {Count} files", batch.Length);
                    totalFailed += batch.Length;
                }
            }

            await _apiClient.UpdateLastScannedAsync(directoryId, cancellationToken);

            _logger.LogInformation(
                "Completed indexing for directory {Path}: {Scanned} scanned, {Processed} processed, {Ingested} ingested, {Failed} failed",
                directoryPath,
                scannedFiles.Count,
                processedFiles.Count,
                totalIngested,
                totalFailed);

            activity?.SetTag("files.ingested", totalIngested);
            activity?.SetTag("files.failed", totalFailed);

            return job.Complete(scannedFiles.Count, processedFiles.Count, totalIngested, totalFailed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index directory {Path}", directoryPath);
            activity?.SetTag("error", true);
            return job.Fail($"Indexing failed: {ex.Message}");
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
