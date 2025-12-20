# 004: Indexing Worker Integration

**Status**: ✅ Complete
**PR**: [#9](https://github.com/gbolabs/photos-index/pull/9)
**Priority**: P1 (Core Features)
**Agent**: A2
**Branch**: `feature/indexing-worker`
**Estimated Complexity**: High

## Objective

Integrate FileScanner, HashComputer, and MetadataExtractor into the Worker service with API integration, progress tracking, and change detection.

## Dependencies

- `03-indexing-service/001-file-scanner.md`
- `03-indexing-service/002-hash-computer.md`
- `03-indexing-service/003-metadata-extractor.md`
- `02-api-layer/001-scan-directories.md`
- `02-api-layer/002-indexed-files.md`

## Acceptance Criteria

- [ ] Poll API for enabled scan directories
- [ ] Process directories in order of last scan (oldest first)
- [ ] Detect new, modified, and deleted files
- [ ] Batch send results to API
- [ ] Track progress and report to API
- [ ] Handle API failures with retry
- [ ] Graceful shutdown on cancellation
- [ ] Configurable scan interval
- [ ] OpenTelemetry tracing for all operations

## TDD Steps

### Red Phase - Worker Logic
```csharp
// tests/IndexingService.Tests/IndexingWorkerTests.cs
public class IndexingWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_ProcessesEnabledDirectories()
    {
        // Mock API client, file scanner, etc.
        // Verify correct flow
    }

    [Fact]
    public async Task ExecuteAsync_SkipsDisabledDirectories()
    {
    }

    [Fact]
    public async Task ExecuteAsync_DetectsNewFiles()
    {
    }
}
```

### Red Phase - Change Detection
```csharp
[Fact]
public async Task ChangeDetection_IdentifiesModifiedFiles()
{
    // File with newer timestamp than last indexed
}

[Fact]
public async Task ChangeDetection_IdentifiesDeletedFiles()
{
    // File in database but not on disk
}
```

### Green Phase
Implement Worker with full pipeline.

### Refactor Phase
Add batching, optimize API calls.

## Files to Create/Modify

```
src/IndexingService/
├── Worker.cs (modify)
├── Services/
│   ├── IIndexingOrchestrator.cs
│   └── IndexingOrchestrator.cs
├── ApiClient/
│   ├── IPhotosApiClient.cs
│   └── PhotosApiClient.cs
└── Models/
    ├── IndexingJob.cs
    └── ChangeDetectionResult.cs

tests/IndexingService.Tests/
├── IndexingWorkerTests.cs
└── Services/
    └── IndexingOrchestratorTests.cs
```

## Service Implementation

```csharp
public interface IIndexingOrchestrator
{
    Task RunIndexingCycleAsync(CancellationToken cancellationToken);
}

public class IndexingOrchestrator : IIndexingOrchestrator
{
    private readonly IPhotosApiClient _apiClient;
    private readonly IFileScanner _scanner;
    private readonly IHashComputer _hashComputer;
    private readonly IMetadataExtractor _metadataExtractor;
    private readonly ILogger<IndexingOrchestrator> _logger;

    public async Task RunIndexingCycleAsync(CancellationToken cancellationToken)
    {
        var directories = await _apiClient.GetEnabledDirectoriesAsync(cancellationToken);

        foreach (var directory in directories.OrderBy(d => d.LastScanUtc))
        {
            await ProcessDirectoryAsync(directory, cancellationToken);
        }
    }

    private async Task ProcessDirectoryAsync(ScanDirectoryDto directory, CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity("ProcessDirectory");
        activity?.SetTag("directory.path", directory.Path);

        var existingFiles = await _apiClient.GetFilesForDirectoryAsync(directory.Id, ct);
        var existingPaths = existingFiles.ToDictionary(f => f.FilePath, f => f);

        var batch = new List<IndexedFileDto>();

        await foreach (var file in _scanner.ScanAsync(directory, ct))
        {
            if (existingPaths.TryGetValue(file.FullPath, out var existing))
            {
                if (existing.FileModifiedUtc >= file.LastModifiedUtc)
                    continue; // No change
            }

            var hash = await _hashComputer.ComputeAsync(file.FullPath, ct);
            var metadata = await _metadataExtractor.ExtractAsync(file.FullPath, ct);
            var thumbnail = await _metadataExtractor.GenerateThumbnailAsync(file.FullPath, new(), ct);

            batch.Add(CreateDto(file, hash, metadata, thumbnail));

            if (batch.Count >= 100)
            {
                await _apiClient.BatchIngestAsync(batch, directory.Id, ct);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
            await _apiClient.BatchIngestAsync(batch, directory.Id, ct);
    }
}
```

## API Client Implementation

```csharp
public interface IPhotosApiClient
{
    Task<IReadOnlyList<ScanDirectoryDto>> GetEnabledDirectoriesAsync(CancellationToken ct);
    Task<IReadOnlyList<IndexedFileDto>> GetFilesForDirectoryAsync(Guid directoryId, CancellationToken ct);
    Task BatchIngestAsync(IReadOnlyList<IndexedFileDto> files, Guid directoryId, CancellationToken ct);
    Task UpdateDirectoryLastScanAsync(Guid directoryId, DateTime lastScan, CancellationToken ct);
}
```

## Worker Integration

```csharp
public class Worker : BackgroundService
{
    private readonly IIndexingOrchestrator _orchestrator;
    private readonly IOptions<WorkerOptions> _options;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _orchestrator.RunIndexingCycleAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Indexing cycle failed");
            }

            await Task.Delay(_options.Value.ScanInterval, stoppingToken);
        }
    }
}
```

## Configuration

```json
{
  "Worker": {
    "ScanIntervalMinutes": 60,
    "BatchSize": 100,
    "MaxParallelHashes": 4,
    "ApiRetryCount": 3,
    "ApiRetryDelayMs": 1000
  }
}
```

## Test Coverage

- Orchestrator: 85% minimum
- Worker: 80% minimum
- API Client: 90% minimum (mock HTTP)
- Change detection: 100%

## Completion Checklist

- [ ] Create IPhotosApiClient interface
- [ ] Implement PhotosApiClient with HttpClient
- [ ] Create IIndexingOrchestrator interface
- [ ] Implement IndexingOrchestrator with full pipeline
- [ ] Implement change detection logic
- [ ] Add batching for API calls
- [ ] Add retry logic for API failures
- [ ] Update Worker.cs to use orchestrator
- [ ] Add OpenTelemetry activities
- [ ] Add configuration options
- [ ] Write unit tests with mocks
- [ ] Write integration tests
- [ ] All tests passing with coverage met
- [ ] PR created and reviewed
