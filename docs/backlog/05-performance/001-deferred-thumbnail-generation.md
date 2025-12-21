# Deferred Thumbnail Generation

## Problem Statement

The current indexing service generates thumbnails synchronously during the initial scan. For large collections (1M+ files), this causes:
- **Excessive scan time**: 1.1M files takes 1.5+ hours
- **High memory usage**: 600MB+ RAM from loading images
- **High CPU load**: Constant 40% CPU for resize operations
- **No visible progress**: Dashboard shows no results until batches complete

## Current Flow (Inefficient)

```
For each file:
  1. Scan file metadata          [Fast]
  2. Compute SHA256 hash         [I/O bound, parallelized]
  3. Load image with ImageSharp  [Memory intensive]
  4. Extract EXIF metadata       [Fast once loaded]
  5. Generate thumbnail          [CPU + memory intensive - LOADS IMAGE AGAIN]
  6. Batch send to API (100)     [Network bound]
```

The thumbnail generation in `MetadataExtractor.cs` loads the full image:
```csharp
using var image = await Image.LoadAsync<Rgba32>(filePath, cancellationToken);
image.Mutate(x => x.AutoOrient());
image.Mutate(x => x.Resize(new ResizeOptions { ... }));
```

## Proposed Solution: Two-Phase Indexing

### Phase 1: Fast Metadata Indexing (Immediate)

Index file metadata and hashes only - no thumbnail generation:

```
For each file:
  1. Scan file metadata          [Fast]
  2. Compute SHA256 hash         [I/O bound]
  3. Load image for EXIF only    [Can use lighter approach]
  4. Send to API (no thumbnail)  [Fast - smaller payload]
```

**Benefits:**
- 10-50x faster initial indexing
- Immediate visibility of duplicates in dashboard
- Low memory footprint

### Phase 2: Background Thumbnail Generation (Deferred)

Generate thumbnails on-demand or as background task:

**Option A: On-Demand Generation (Recommended)**
```
GET /api/files/{id}/thumbnail
  - If thumbnail exists in cache → return it
  - If not → generate, cache, return
```

**Option B: Background Worker**
```
Separate service/job that:
  1. Queries files without thumbnails
  2. Generates thumbnails in batches
  3. Updates API with thumbnails
  4. Runs with lower priority
```

**Option C: Hybrid**
- On-demand for viewed files
- Background worker for pre-generation during idle

## Implementation Plan

### API Changes

1. **New endpoint**: `GET /api/files/{id}/thumbnail`
   - Returns cached thumbnail if exists
   - Generates on-demand if missing (requires file access)
   - Returns 404 if file not found

2. **New endpoint**: `GET /api/files/without-thumbnails?limit=100`
   - Returns files needing thumbnail generation
   - Used by background worker

3. **Modified batch ingest**: Make `ThumbnailBase64` truly optional
   - Currently optional but always sent
   - Skip thumbnail field entirely in phase 1

### IndexingService Changes

1. **Add configuration option**:
   ```csharp
   public class IndexingOptions
   {
       public bool GenerateThumbnails { get; set; } = false; // Default OFF
       public int ThumbnailBatchSize { get; set; } = 50;
   }
   ```

2. **Separate thumbnail worker** (optional):
   ```csharp
   public class ThumbnailWorker : BackgroundService
   {
       // Runs after initial indexing
       // Lower priority, smaller batches
       // Can be paused/resumed
   }
   ```

3. **Lighter metadata extraction**:
   - Use ExifLib or MetadataExtractor.NET instead of ImageSharp for EXIF-only
   - Only load full image when generating thumbnails

### Database Changes

1. **Add column**: `IndexedFiles.HasThumbnail` (boolean)
   - Allows efficient querying of files without thumbnails

2. **Index**: On `HasThumbnail` for background worker queries

### Web UI Changes

1. **Placeholder thumbnails**: Show generic icon while loading
2. **Lazy loading**: Only request thumbnails for visible files
3. **Loading indicator**: Show generation progress

## Performance Estimates

| Scenario | Current | Phase 1 Only | With Background |
|----------|---------|--------------|-----------------|
| 1.1M files initial scan | 1.5+ hours | ~10-15 min | ~10-15 min |
| Memory usage | 600MB+ | ~100MB | ~200MB |
| CPU load | 40% constant | 10-20% | 5-10% bg |
| Time to first results | 1.5+ hours | ~2 min | ~2 min |

## Migration Strategy

1. **Immediate**: Add config flag to disable thumbnail generation
2. **Short-term**: Implement on-demand thumbnail endpoint
3. **Medium-term**: Add background thumbnail worker
4. **Optional**: Implement thumbnail caching layer (Redis/disk)

## Configuration

### Environment Variables

```bash
# Disable thumbnails during initial scan (recommended for large collections)
GENERATE_THUMBNAILS=false

# Background thumbnail generation
THUMBNAIL_WORKER_ENABLED=true
THUMBNAIL_BATCH_SIZE=50
THUMBNAIL_WORKER_INTERVAL_SECONDS=30
```

### Docker Compose

```yaml
indexer:
  environment:
    - GENERATE_THUMBNAILS=false  # Fast initial scan

thumbnail-worker:  # Optional separate service
  image: ghcr.io/gbolabs/photos-index/indexing-service:0.0.1
  environment:
    - WORKER_MODE=thumbnails
    - THUMBNAIL_BATCH_SIZE=50
```

## Acceptance Criteria

- [ ] Initial indexing completes in <20 min for 1M files
- [ ] Dashboard shows files immediately (without thumbnails)
- [ ] Thumbnails load on-demand when viewing files
- [ ] Memory usage stays under 200MB during indexing
- [ ] Background worker generates thumbnails without blocking scans
- [ ] Configuration allows enabling/disabling thumbnail generation

## Related Issues

- High memory usage during indexing
- Slow initial scan time
- No progress visibility during long scans

## Priority

**High** - Blocking real-world usage with large photo collections

## Effort Estimate

- Phase 1 (disable thumbnails): 2-4 hours
- On-demand endpoint: 4-8 hours
- Background worker: 8-16 hours
- Full implementation: 2-3 days
