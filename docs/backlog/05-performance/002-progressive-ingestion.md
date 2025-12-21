# Progressive File Ingestion During Scan

## Problem Statement

Currently, the indexer scans ALL files first, then processes them in a second phase. For 1.1M+ files, this means:
- No data visible in dashboard until scan completes
- Large memory usage to hold all scanned file metadata
- Long wait before any results appear

## Current Flow

```
1. Scan ALL files (1.1M entries)     → ~5-10 min, holds all in memory
2. Compute hashes for ALL files      → parallelized
3. Extract metadata for ALL files    → sequential
4. Batch ingest ALL to API           → 100 at a time
```

## Proposed Flow

```
1. Scan files in batches of 1000
2. For each batch:
   - Compute hashes (parallel)
   - Extract metadata
   - Ingest to API immediately
   - Report progress
3. Continue until directory exhausted
```

## Benefits

- **Immediate visibility**: First files appear in dashboard within seconds
- **Lower memory**: Only hold current batch in memory
- **Progress feedback**: Users see file count increasing in real-time
- **Resumability**: Can track progress and resume after crash

## Implementation

### IndexingOrchestrator Changes

```csharp
public async Task<IndexingJob> IndexDirectoryAsync(...)
{
    const int ScanBatchSize = 1000;
    var batch = new List<ScannedFile>(ScanBatchSize);

    await foreach (var file in _fileScanner.ScanAsync(directoryPath, ...))
    {
        batch.Add(file);

        if (batch.Count >= ScanBatchSize)
        {
            await ProcessAndIngestBatchAsync(batch, ...);
            _logger.LogInformation("Ingested batch of {Count} files", batch.Count);
            batch.Clear();
        }
    }

    // Process remaining files
    if (batch.Count > 0)
    {
        await ProcessAndIngestBatchAsync(batch, ...);
    }
}

private async Task ProcessAndIngestBatchAsync(List<ScannedFile> files, ...)
{
    // 1. Compute hashes
    // 2. Extract metadata
    // 3. Ingest to API
}
```

### Configuration

```bash
# Batch size for progressive ingestion
SCAN_BATCH_SIZE=1000
```

### Database Considerations

- API already handles duplicates via `OnConflict` UPSERT
- No changes needed on API side
- Progress can be tracked by checking file count in database

## Metrics

| Scenario | Current | Progressive |
|----------|---------|-------------|
| Time to first result | 1+ hours | ~10 seconds |
| Memory usage | 2GB+ | ~200MB |
| Dashboard visibility | After complete | Immediate |

## Priority

**High** - Significant UX improvement for large collections

## Effort

4-8 hours
