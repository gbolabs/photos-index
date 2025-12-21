# 09: Thumbnail Generation Offload to MPC

## Problem Statement

The Synology NAS running IndexingService has limited CPU resources. Thumbnail generation is compute-intensive (image decode, resize, JPEG encode). The MPC (TrueNAS running API) is mostly idle and has better compute capacity.

**Current constraints:**
- Synology: Low-power Intel Celeron J3455, limited RAM
- MPC/TrueNAS: **AMD Ryzen 5 5500U, 32GB RAM (15GB free)** - significantly more capable
- Network: 1Gbit/s direct connection (same switch, no hops)
- Thumbnails currently disabled (`GENERATE_THUMBNAILS=false`) due to Synology performance

**MPC Capacity Analysis:**
| Resource | Available | Thumbnail Processing |
|----------|-----------|---------------------|
| CPU | 6 cores / 12 threads | Can run 8+ parallel workers |
| RAM | 15 GB free | ~50MB per worker = 8 workers use 400MB |
| Disk I/O | NVMe/SSD | Fast temp file writes |

The Ryzen 5 5500U can easily handle 10+ thumbnails/second with parallel processing.

## Proposed Solution

Offload thumbnail generation from IndexingService (Synology) to a background worker on the API side (MPC).

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           SYNOLOGY NAS                                   │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │  IndexingService                                                 │    │
│  │  - Scan files                                                    │    │
│  │  - Compute SHA256 hash                                           │    │
│  │  - Extract EXIF metadata                                         │    │
│  │  - Read image bytes (for new/changed files)                      │    │
│  │  - Send batch with image data to API                             │    │
│  └──────────────────────────┬──────────────────────────────────────┘    │
└─────────────────────────────┼───────────────────────────────────────────┘
                              │ HTTP POST /api/files/batch
                              │ (metadata + image bytes)
                              │ ~100MB/s theoretical
                              ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                           MPC (TrueNAS)                                  │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │  API Service                                                     │    │
│  │  1. Validate & store metadata in PostgreSQL                      │    │
│  │  2. Save image bytes to temp directory                           │    │
│  │  3. Create ThumbnailJob record (pending)                         │    │
│  │  4. Return success immediately                                   │    │
│  └──────────────────────────┬──────────────────────────────────────┘    │
│                              │                                           │
│                              ▼                                           │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │  ThumbnailWorker (BackgroundService)                             │    │
│  │  - Poll for pending ThumbnailJobs                                │    │
│  │  - Load image from temp directory                                │    │
│  │  - Generate thumbnail (200x200 JPEG)                             │    │
│  │  - Save thumbnail to thumbnails directory                        │    │
│  │  - Update IndexedFile.ThumbnailPath                              │    │
│  │  - Delete temp image                                             │    │
│  │  - Mark job complete                                             │    │
│  └─────────────────────────────────────────────────────────────────┘    │
│                                                                          │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │  PostgreSQL                                                      │    │
│  │  - IndexedFiles table (existing)                                 │    │
│  │  - ThumbnailJobs table (NEW - durable queue)                     │    │
│  └─────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────┘
```

## Network Bandwidth Analysis

| Metric | Value |
|--------|-------|
| Link speed | 1 Gbit/s |
| Practical throughput | ~100 MB/s |
| Average JPEG size | 3-5 MB |
| Average HEIC size | 2-3 MB |
| Files per second | ~20-30 |
| Batch size (100 files) | ~300-500 MB |
| Batch transfer time | ~3-5 seconds |

**Conclusion:** Network bandwidth is sufficient. A batch of 100 images (~400MB) transfers in ~4 seconds.

## Database Schema Changes

### New Table: ThumbnailJobs

```sql
CREATE TABLE "ThumbnailJobs" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "IndexedFileId" uuid NOT NULL REFERENCES "IndexedFiles"("Id") ON DELETE CASCADE,
    "TempImagePath" varchar(1000) NOT NULL,
    "Status" varchar(20) NOT NULL DEFAULT 'Pending',  -- Pending, Processing, Completed, Failed
    "CreatedAt" timestamptz NOT NULL DEFAULT now(),
    "StartedAt" timestamptz NULL,
    "CompletedAt" timestamptz NULL,
    "ErrorMessage" text NULL,
    "RetryCount" int NOT NULL DEFAULT 0
);

CREATE INDEX "IX_ThumbnailJobs_Status" ON "ThumbnailJobs" ("Status") WHERE "Status" = 'Pending';
CREATE INDEX "IX_ThumbnailJobs_IndexedFileId" ON "ThumbnailJobs" ("IndexedFileId");
```

### Job States

```
Pending ──► Processing ──► Completed
                │
                └──► Failed (after max retries)
```

## API Changes

### Modified Endpoint: POST /api/files/batch

**Option A: Base64 in JSON (simpler, current approach)**
```json
{
  "scanDirectoryId": "guid",
  "files": [
    {
      "filePath": "/photos/2024/img001.jpg",
      "fileName": "img001.jpg",
      "fileHash": "sha256...",
      "fileSize": 4500000,
      "width": 4032,
      "height": 3024,
      "modifiedAt": "2024-01-15T10:30:00Z",
      "imageDataBase64": "base64-encoded-image-bytes..."
    }
  ]
}
```

**Option B: Multipart form (more efficient for large payloads)**
```
POST /api/files/batch
Content-Type: multipart/form-data

--boundary
Content-Disposition: form-data; name="metadata"
Content-Type: application/json

{"scanDirectoryId":"guid","files":[...]}

--boundary
Content-Disposition: form-data; name="images[0]"; filename="sha256hash1.jpg"
Content-Type: image/jpeg

<binary image data>

--boundary
Content-Disposition: form-data; name="images[1]"; filename="sha256hash2.heic"
Content-Type: image/heic

<binary image data>
--boundary--
```

**Recommendation:** Start with Option A (Base64) for simplicity. Migrate to Option B if performance becomes an issue (Base64 adds ~33% overhead).

### New Endpoint: GET /api/thumbnails/status

Returns thumbnail generation queue status for monitoring.

```json
{
  "pending": 1250,
  "processing": 4,
  "completedLast24h": 15000,
  "failedLast24h": 12,
  "avgProcessingTimeMs": 150
}
```

## ThumbnailWorker Design

### Configuration

```csharp
public class ThumbnailWorkerOptions
{
    public int PollingIntervalSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 10;           // Jobs per poll
    public int MaxParallelism { get; set; } = 4;       // Concurrent processing
    public int MaxRetries { get; set; } = 3;
    public int ThumbnailMaxWidth { get; set; } = 200;
    public int ThumbnailMaxHeight { get; set; } = 200;
    public int ThumbnailQuality { get; set; } = 80;
}
```

### Processing Loop

```csharp
public class ThumbnailWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var jobs = await _jobRepository.GetPendingJobsAsync(_options.BatchSize, ct);

            if (jobs.Any())
            {
                await Parallel.ForEachAsync(jobs,
                    new ParallelOptions { MaxDegreeOfParallelism = _options.MaxParallelism },
                    async (job, token) => await ProcessJobAsync(job, token));
            }
            else
            {
                await Task.Delay(_options.PollingIntervalSeconds * 1000, ct);
            }
        }
    }

    private async Task ProcessJobAsync(ThumbnailJob job, CancellationToken ct)
    {
        try
        {
            await _jobRepository.MarkProcessingAsync(job.Id);

            // Load temp image
            using var image = await Image.LoadAsync(job.TempImagePath, ct);

            // Generate thumbnail
            var thumbnail = await _thumbnailGenerator.GenerateAsync(image, _options);

            // Save thumbnail
            var thumbnailPath = await _thumbnailStorage.SaveAsync(job.IndexedFileId, thumbnail);

            // Update IndexedFile
            await _fileRepository.UpdateThumbnailPathAsync(job.IndexedFileId, thumbnailPath);

            // Cleanup temp file
            File.Delete(job.TempImagePath);

            // Mark complete
            await _jobRepository.MarkCompletedAsync(job.Id);
        }
        catch (Exception ex)
        {
            await _jobRepository.MarkFailedAsync(job.Id, ex.Message, job.RetryCount + 1);
            _logger.LogError(ex, "Failed to process thumbnail job {JobId}", job.Id);
        }
    }
}
```

## Crash Recovery

### Scenario 1: API crashes after saving temp image, before creating job
- **Detection:** Temp images without corresponding ThumbnailJob
- **Recovery:** Startup task scans temp directory, creates missing jobs

### Scenario 2: Worker crashes during processing
- **Detection:** Jobs stuck in "Processing" state for > 5 minutes
- **Recovery:** Startup task resets stale "Processing" jobs to "Pending"

### Scenario 3: Temp image deleted before processing
- **Detection:** Job fails with "file not found"
- **Recovery:** Mark job as failed with clear error, file will be re-indexed next cycle

### Startup Recovery Task

```csharp
public class ThumbnailRecoveryService : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        // Reset stale processing jobs (stuck > 5 min)
        await _jobRepository.ResetStaleJobsAsync(TimeSpan.FromMinutes(5));

        // Find orphaned temp files
        var orphanedFiles = await FindOrphanedTempFilesAsync();
        foreach (var file in orphanedFiles)
        {
            // Either create job or delete if too old
            if (File.GetCreationTime(file) > DateTime.Now.AddDays(-1))
            {
                await CreateJobForOrphanedFileAsync(file);
            }
            else
            {
                File.Delete(file);
            }
        }
    }
}
```

## Monitoring & Observability

### Metrics (OpenTelemetry)

| Metric | Type | Description |
|--------|------|-------------|
| `thumbnail_jobs_pending` | Gauge | Current pending jobs |
| `thumbnail_jobs_processing` | Gauge | Currently processing |
| `thumbnail_jobs_completed_total` | Counter | Total completed |
| `thumbnail_jobs_failed_total` | Counter | Total failed |
| `thumbnail_processing_duration_ms` | Histogram | Processing time |
| `thumbnail_queue_wait_time_ms` | Histogram | Time in queue |

### Health Check

```csharp
public class ThumbnailHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(...)
    {
        var pending = await _jobRepository.GetPendingCountAsync();
        var stale = await _jobRepository.GetStaleProcessingCountAsync(TimeSpan.FromMinutes(10));

        if (stale > 0)
            return HealthCheckResult.Degraded($"{stale} stale jobs");
        if (pending > 10000)
            return HealthCheckResult.Degraded($"{pending} pending jobs (backlog)");

        return HealthCheckResult.Healthy($"{pending} pending");
    }
}
```

## Implementation Tasks

### Phase 1: Database & Models
- [ ] Create `ThumbnailJob` entity
- [ ] Add EF Core migration
- [ ] Create `IThumbnailJobRepository`

### Phase 2: API Changes
- [ ] Modify `BatchIngestFilesRequest` to accept image data
- [ ] Update `IndexedFileService.BatchIngestAsync()` to:
  - Save temp images
  - Create ThumbnailJob records
- [ ] Add temp image cleanup on API shutdown

### Phase 3: Background Worker
- [ ] Create `ThumbnailWorker` BackgroundService
- [ ] Create `ThumbnailGenerator` service (extract from IndexingService)
- [ ] Create `ThumbnailRecoveryService`
- [ ] Add configuration options

### Phase 4: IndexingService Changes
- [ ] Add image reading to `MetadataExtractor`
- [ ] Modify batch payload to include image bytes
- [ ] Add `OFFLOAD_THUMBNAILS` config option (default: true)
- [ ] Increase batch size for network efficiency

### Phase 5: Monitoring
- [ ] Add OpenTelemetry metrics
- [ ] Add health check endpoint
- [ ] Add `/api/thumbnails/status` endpoint
- [ ] Update Aspire dashboard

### Phase 6: Testing & Documentation
- [ ] Unit tests for ThumbnailWorker
- [ ] Integration tests with TestContainers
- [ ] Load testing with realistic batch sizes
- [ ] Update deployment documentation

## Configuration

### IndexingService (Synology)

```env
# Enable thumbnail offloading (sends images to API)
OFFLOAD_THUMBNAILS=true

# Larger batches for network efficiency
BATCH_SIZE=50

# Disable local thumbnail generation
GENERATE_THUMBNAILS=false
```

### API (MPC/TrueNAS)

```env
# Temp storage for incoming images
THUMBNAIL_TEMP_DIR=/app/temp/images

# Final thumbnail storage
THUMBNAIL_DIR=/app/thumbnails

# Worker settings
THUMBNAIL_WORKER_PARALLELISM=4
THUMBNAIL_WORKER_BATCH_SIZE=10
THUMBNAIL_MAX_RETRIES=3
```

## Rollback Plan

If issues arise:
1. Set `OFFLOAD_THUMBNAILS=false` on IndexingService
2. Set `GENERATE_THUMBNAILS=true` on IndexingService
3. Stop ThumbnailWorker on API
4. System reverts to original behavior

No data migration needed - thumbnails are regenerated.

## Recommended Architecture: Dedicated ThumbnailService

Instead of running thumbnail processing inside the API container, create a dedicated `ThumbnailService`:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           MPC (TrueNAS)                                  │
│  Ryzen 5 5500U (6c/12t), 32GB RAM                                       │
│                                                                          │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐      │
│  │   API Service   │    │  Message Bus    │    │ThumbnailService │      │
│  │                 │───▶│  (PostgreSQL    │───▶│                 │      │
│  │ POST /files     │    │   or RabbitMQ)  │    │ - Consumes      │      │
│  │ → Save temp     │    │                 │    │   messages      │      │
│  │ → Publish msg   │    │ ThumbnailQueue  │    │ - Generates     │      │
│  └─────────────────┘    └─────────────────┘    │   thumbnails    │      │
│                                                 │ - Scales 0-N    │      │
│                                                 └─────────────────┘      │
│                                                                          │
│  ┌─────────────────┐    ┌─────────────────┐                             │
│  │   PostgreSQL    │    │  Temp Storage   │                             │
│  │                 │    │  /app/temp      │                             │
│  │ - IndexedFiles  │    │  (shared vol)   │                             │
│  │ - ThumbnailJobs │    │                 │                             │
│  └─────────────────┘    └─────────────────┘                             │
└─────────────────────────────────────────────────────────────────────────┘
```

### ThumbnailService Container

```yaml
# docker-compose.yml
thumbnail-service:
  image: ghcr.io/gbolabs/photos-index/thumbnail-service:latest
  environment:
    - OTEL_SERVICE_NAME=photos-index-thumbnail
    - MESSAGE_BUS_CONNECTION=amqp://rabbitmq:5672
    - THUMBNAIL_PARALLELISM=8
    - THUMBNAIL_MAX_WIDTH=200
    - THUMBNAIL_MAX_HEIGHT=200
  volumes:
    - temp-images:/app/temp:ro      # Read temp images
    - thumbnails:/app/thumbnails    # Write thumbnails
  deploy:
    resources:
      limits:
        cpus: '4'
        memory: 2G
    replicas: 1  # Can scale to 2-3 if needed
  depends_on:
    - rabbitmq
    - postgres
```

### Benefits of Dedicated Service

| Aspect | In-API Worker | Dedicated ThumbnailService |
|--------|---------------|---------------------------|
| API latency | May be impacted during heavy processing | Unaffected |
| Scaling | Scales with API | Independent scaling |
| Restart | Restarts API | Independent restart |
| Resource limits | Shares with API | Dedicated limits |
| Monitoring | Mixed with API metrics | Clean separation |
| Message bus | Optional | Required (good!) |

### New Project Structure

```
src/
├── ThumbnailService/           # NEW
│   ├── ThumbnailService.csproj
│   ├── Program.cs
│   ├── Worker/
│   │   └── ThumbnailWorker.cs
│   ├── Services/
│   │   ├── ThumbnailGenerator.cs
│   │   └── MessageConsumer.cs
│   └── appsettings.json
└── Shared/
    └── Messages/
        ├── ThumbnailRequestedMessage.cs
        ├── ThumbnailCompletedMessage.cs
        └── ThumbnailFailedMessage.cs
```

## Relationship to Service Bus (09-fixes/003)

This feature should leverage the generic **Service Bus infrastructure** defined in `docs/backlog/09-fixes/003-service-bus-communication.md`.

### Message Types for Thumbnail Processing

```csharp
// Shared.Messages/ThumbnailRequestedMessage.cs
public record ThumbnailRequestedMessage
{
    public Guid IndexedFileId { get; init; }
    public string TempImagePath { get; init; }
    public DateTime RequestedAt { get; init; }
}

// Shared.Messages/ThumbnailCompletedMessage.cs
public record ThumbnailCompletedMessage
{
    public Guid IndexedFileId { get; init; }
    public string ThumbnailPath { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public long SizeBytes { get; init; }
    public TimeSpan ProcessingTime { get; init; }
}

// Shared.Messages/ThumbnailFailedMessage.cs
public record ThumbnailFailedMessage
{
    public Guid IndexedFileId { get; init; }
    public string Error { get; init; }
    public int RetryCount { get; init; }
}
```

### Integration with Service Bus

If Service Bus (RabbitMQ/PostgreSQL queue) is implemented:
- Replace `ThumbnailJobs` table polling with message consumption
- `ThumbnailRequestedMessage` → Worker processes thumbnail
- `ThumbnailCompletedMessage` → Update IndexedFile, notify UI via SignalR
- `ThumbnailFailedMessage` → Dead letter queue, retry logic

This makes the bus reusable for future scenarios:
- File deletion requests (CleanerService)
- Scan triggers (Scan Now button)
- Duplicate detection notifications
- Real-time UI updates

## Alternatives Considered

### 1. Message Queue (RabbitMQ/Redis)
- **Pros:** Purpose-built for queuing, better throughput
- **Cons:** Additional infrastructure, operational overhead
- **Decision:** Use if Service Bus (09-003) is implemented; otherwise PostgreSQL queue

### 2. On-demand thumbnail generation
- **Pros:** No queue, simpler architecture
- **Cons:** Latency on first view, repeated work if not cached
- **Decision:** Pre-generation provides better UX

### 3. Separate ThumbnailService container (RECOMMENDED)
- **Pros:**
  - Horizontal scaling (run multiple instances)
  - Isolation (doesn't impact API performance)
  - Independent resource limits (CPU/memory)
  - Clean restart without affecting API
  - Natural fit for message bus architecture
- **Cons:**
  - One more container to deploy
  - Slightly more network overhead
- **Decision:** **Preferred approach** - aligns with Service Bus (09-003), clean separation of concerns

## Success Criteria

- [ ] Thumbnails generated without impacting Synology CPU
- [ ] Processing rate: >10 thumbnails/second
- [ ] Queue backlog clears within 1 hour for 100k files
- [ ] Zero data loss on crashes/restarts
- [ ] Observability in Aspire dashboard
