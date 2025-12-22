# 004: Indexing Performance Optimizations

## Problem Statement

Indexing 1.1M files takes too long. Synology NAS shows:
- ~50% CPU utilization (underutilized)
- No significant network I/O
- No volume I/O spikes

The indexer is not fully utilizing available resources.

## Current Bottlenecks

### 1. Sequential Metadata Extraction (CRITICAL)

**File**: `IndexingOrchestrator.cs` lines 193-229

```csharp
// CURRENT: Sequential - processes one file at a time
foreach (var scannedFile in batch)
{
    var metadata = await _metadataExtractor.ExtractAsync(...);  // BLOCKING
    // ... thumbnail generation also sequential
}
```

**Impact**: With 100 files per batch at 50ms each = 5 seconds of serial work.

### 2. Hash Parallelism Limited to 4

Hash computation is I/O bound, not CPU bound. Can safely exceed core count.

### 3. No Skip for Unchanged Files

Files that haven't changed since last scan are still re-hashed.

### 4. Small Batch Size

100 files per batch = more API round trips. Network overhead adds up.

## Proposed Optimizations

### Optimization 1: Parallel Metadata Extraction

```csharp
// AFTER: Parallel metadata extraction
var processedFiles = new ConcurrentBag<ProcessedFile>();

await Parallel.ForEachAsync(
    batch.Where(f => hashResults.ContainsKey(f.FullPath) && hashResults[f.FullPath].Success),
    new ParallelOptions { MaxDegreeOfParallelism = _options.MaxParallelism },
    async (scannedFile, ct) =>
    {
        var hashResult = hashResults[scannedFile.FullPath];
        try
        {
            var metadata = await _metadataExtractor.ExtractAsync(scannedFile.FullPath, ct);

            byte[]? thumbnail = null;
            if (_options.GenerateThumbnails)
            {
                try
                {
                    thumbnail = await _metadataExtractor.GenerateThumbnailAsync(...);
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
```

**Expected improvement**: 4-8x faster metadata extraction.

### Optimization 2: Skip Unchanged Files

Add modification time tracking to skip files that haven't changed:

```csharp
// In API: Track last indexed timestamp per file
public DateTime? LastIndexedAt { get; set; }

// In Indexer: Skip if file hasn't changed
if (file.LastModifiedUtc <= lastIndexedAt)
{
    _logger.LogDebug("Skipping unchanged file: {Path}", file.FullPath);
    continue;
}
```

**API Changes Needed**:
- Add `GET /api/files/modified-since?directoryId={id}&since={datetime}` endpoint
- Return list of file paths that need re-indexing

**Expected improvement**: 90%+ reduction on subsequent scans.

### Optimization 3: Increase Default Parallelism

```csharp
public class IndexingOptions
{
    // Change default from 4 to Environment.ProcessorCount * 2
    public int MaxParallelism { get; set; } = Math.Max(4, Environment.ProcessorCount * 2);

    // Change default from 100 to 250
    public int BatchSize { get; set; } = 250;
}
```

For Celeron J3455 (4 cores): `MaxParallelism = 8`

### Optimization 4: Pipelined Processing

Instead of:
```
Scan → Wait → Hash → Wait → Metadata → Wait → API
```

Use producer-consumer pipeline:
```
Scan ──┬──► Hash Worker 1 ──┬──► Metadata Worker 1 ──┬──► API Batcher
       ├──► Hash Worker 2 ──┤──► Metadata Worker 2 ──┤
       ├──► Hash Worker 3 ──┤──► Metadata Worker 3 ──┤
       └──► Hash Worker 4 ──┘──► Metadata Worker 4 ──┘
```

```csharp
// Pipeline with channels
var scanChannel = Channel.CreateBounded<ScannedFile>(500);
var hashChannel = Channel.CreateBounded<(ScannedFile, HashResult)>(200);
var processedChannel = Channel.CreateBounded<ProcessedFile>(200);

// Producer: Scanner
var scanTask = Task.Run(async () => {
    await foreach (var file in _fileScanner.ScanAsync(...))
        await scanChannel.Writer.WriteAsync(file);
    scanChannel.Writer.Complete();
});

// Stage 1: Hashers (N workers)
var hashTasks = Enumerable.Range(0, _options.MaxParallelism).Select(_ => Task.Run(async () => {
    await foreach (var file in scanChannel.Reader.ReadAllAsync())
    {
        var hash = await _hashComputer.ComputeAsync(file.FullPath);
        await hashChannel.Writer.WriteAsync((file, hash));
    }
}));

// Stage 2: Metadata extractors (N workers)
var metadataTasks = Enumerable.Range(0, _options.MaxParallelism).Select(_ => Task.Run(async () => {
    await foreach (var (file, hash) in hashChannel.Reader.ReadAllAsync())
    {
        var metadata = await _metadataExtractor.ExtractAsync(file.FullPath);
        await processedChannel.Writer.WriteAsync(new ProcessedFile { ... });
    }
}));

// Consumer: API batcher
var batchTask = Task.Run(async () => {
    var batch = new List<ProcessedFile>();
    await foreach (var processed in processedChannel.Reader.ReadAllAsync())
    {
        batch.Add(processed);
        if (batch.Count >= _options.BatchSize)
        {
            await IngestBatchAsync(batch);
            batch.Clear();
        }
    }
    if (batch.Count > 0) await IngestBatchAsync(batch);
});
```

**Expected improvement**: 2-3x overall throughput by overlapping I/O.

### Optimization 5: Increase Buffer Size for NAS

The NAS likely has slower disk I/O. Increase buffer size:

```csharp
// Current: 80KB buffer
private const int BufferSize = 81920;

// Proposed: 256KB buffer for NAS spinning disks
private const int BufferSize = 262144;
```

## Quick Wins (Environment Variables Only)

No code changes, just configuration:

```env
# Synology docker-compose.yml
MAX_PARALLELISM=8          # Up from 4
BATCH_SIZE=250             # Up from 100
```

**Expected improvement**: 30-50% faster with no code changes.

## Optimization 6: Offload Everything to MPC (RECOMMENDED)

The most impactful optimization: **offload all compute to MPC**.

### Current Flow (Compute-Heavy on Synology)
```
Synology (Celeron J3455)           MPC (Ryzen 5 5500U)
─────────────────────────          ────────────────────
1. Scan files
2. Compute SHA256 hash    ←SLOW
3. Extract EXIF metadata  ←SLOW
4. Generate thumbnail     ←SLOW
5. Send metadata + thumb  ────────► Store in DB
```

### Proposed Flow (Compute on MPC)
```
Synology (Celeron J3455)           MPC (Ryzen 5 5500U)
─────────────────────────          ────────────────────
1. Scan files
2. Read file bytes
3. Send file bytes       ─────────► 4. Compute SHA256 hash
                                    5. Extract EXIF metadata
                                    6. Generate thumbnail
                                    7. Store in DB
                                    8. Delete temp file
```

### Benefits

| Aspect | Current | Offloaded |
|--------|---------|-----------|
| Synology CPU | 50-80% | <10% |
| Synology RAM | 200-500MB | <50MB |
| Network utilization | Low | High (good!) |
| Overall throughput | ~5 files/sec | ~50+ files/sec |
| Complexity | All in indexer | Split across services |

### Indexer Becomes Ultra-Simple

```csharp
public async Task IndexDirectoryAsync(Guid directoryId, string path, CancellationToken ct)
{
    await foreach (var file in _fileScanner.ScanAsync(path, true, ct))
    {
        currentBatch.Add(file);
        if (currentBatch.Count >= _options.BatchSize)
        {
            // Just read bytes and send - no processing!
            var payload = await BuildPayloadAsync(currentBatch, ct);
            await _apiClient.ProcessFilesAsync(payload, ct);
            currentBatch.Clear();
        }
    }
}

private async Task<ProcessFilesRequest> BuildPayloadAsync(List<ScannedFile> files, CancellationToken ct)
{
    var items = new List<FilePayload>();
    foreach (var file in files)
    {
        var bytes = await File.ReadAllBytesAsync(file.FullPath, ct);
        items.Add(new FilePayload
        {
            FilePath = file.FullPath,
            FileName = file.FileName,
            FileSize = file.FileSizeBytes,
            ModifiedAt = file.LastModifiedUtc,
            FileData = bytes  // Raw image bytes
        });
    }
    return new ProcessFilesRequest { DirectoryId = directoryId, Files = items };
}
```

### MPC Processing Service

Create a `FileProcessingService` that handles everything:

```csharp
public class FileProcessingService
{
    public async Task<ProcessedFile> ProcessAsync(FilePayload payload, CancellationToken ct)
    {
        // All compute happens here on the Ryzen!

        // 1. Compute hash
        using var sha256 = SHA256.Create();
        var hash = Convert.ToHexString(sha256.ComputeHash(payload.FileData));

        // 2. Extract metadata
        using var stream = new MemoryStream(payload.FileData);
        using var image = await Image.LoadAsync(stream, ct);
        var metadata = ExtractExifMetadata(image);

        // 3. Generate thumbnail
        var thumbnail = await GenerateThumbnailAsync(image);

        return new ProcessedFile { Hash = hash, Metadata = metadata, Thumbnail = thumbnail };
    }
}
```

### Network Bandwidth Check

| Metric | Value |
|--------|-------|
| Link speed | 1 Gbit/s = 125 MB/s |
| Average file size | 4 MB |
| Files per second | 125 / 4 = ~30 files/sec |
| With overhead | ~20-25 files/sec sustained |

**Result**: Network can handle 20-25 files/sec, which is 4-5x faster than current.

### Relationship to Thumbnail Offload (09-004)

This is a **superset** of the thumbnail offload feature:
- Thumbnail offload: Send images for thumbnail generation
- Full offload: Send images for hash + metadata + thumbnail

Can be implemented incrementally:
1. First: Offload thumbnails (09-004)
2. Then: Offload metadata extraction
3. Finally: Offload hash computation

Or implement all at once for maximum benefit.

## Implementation Priority

| Optimization | Effort | Impact | Priority |
|-------------|--------|--------|----------|
| Increase MAX_PARALLELISM to 8 | Config only | 20-30% | P0 - Do now |
| Increase BATCH_SIZE to 250 | Config only | 10-20% | P0 - Do now |
| **Offload all compute to MPC** | 2-3 days | **10x** | **P0 - Best ROI** |
| Parallel metadata extraction | 1 hour | 3-5x | P1 - If not offloading |
| Skip unchanged files | 4 hours | 90% on re-scans | P1 - Next PR |
| Pipelined processing | 8 hours | 2-3x | P2 - Future |
| Increase buffer size | 5 min | 5-10% | P1 - Next PR |

## Metrics to Track

Add OpenTelemetry metrics:

```csharp
// Timing per phase
indexing_scan_duration_seconds
indexing_hash_duration_seconds
indexing_metadata_duration_seconds
indexing_api_duration_seconds

// Throughput
indexing_files_per_second
indexing_bytes_per_second

// Skip stats
indexing_files_skipped_total
indexing_files_processed_total
```

## Success Criteria

- [ ] Initial scan of 1.1M files completes in < 2 hours (vs current 8+ hours)
- [ ] Subsequent scans complete in < 15 minutes (skip unchanged)
- [ ] CPU utilization reaches 80%+ during indexing
- [ ] Memory stays under 500MB
