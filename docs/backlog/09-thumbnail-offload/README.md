# 09: Distributed Processing - Thumbnail & Metadata Offload

**Status**: 🔲 Not Started
**Priority**: P1.5
**Agent**: A1/A2
**Related ADR**: [ADR-004: Distributed Processing Architecture](../../adrs/004-distributed-processing-architecture.md)

## Problem Statement

The Synology NAS running IndexingService has limited CPU resources. Thumbnail generation and EXIF metadata extraction are compute-intensive (image decode, resize, EXIF parsing). The MPC (TrueNAS running API) is mostly idle and has better compute capacity.

**Current constraints:**
- Synology: Low-power Intel Celeron J3455, limited RAM
- MPC/TrueNAS: **AMD Ryzen 5 5500U, 32GB RAM (15GB free)** - significantly more capable
- Network: 1Gbit/s direct connection (same switch, no hops)
- Thumbnails currently disabled (`GENERATE_THUMBNAILS=false`) due to Synology performance

**MPC Capacity Analysis:**
| Resource | Available | Processing Capacity |
|----------|-----------|---------------------|
| CPU | 6 cores / 12 threads | Can run 8+ parallel workers |
| RAM | 15 GB free | ~50MB per worker = 8 workers use 400MB |
| Disk I/O | NVMe/SSD | Fast temp file writes |

The Ryzen 5 5500U can easily handle 10+ thumbnails/second with parallel processing.

## Solution: Event-Driven Distributed Processing

Offload thumbnail generation and metadata extraction from IndexingService to dedicated processing services running on MPC, using asynchronous messaging and object storage.

### Architecture Overview

```
IndexingService (Synology - MINIMAL responsibilities)
    |
    | HTTP POST /api/files/ingest (multipart: metadata + image bytes)
    | (Single communication channel - no message bus dependency)
    v
+------------------------------------------------------------------+
| API Service (Gateway + SOLE DB OWNER)                             |
|                                                                   |
|  INGEST FLOW:                                                     |
|  1. Receive file from IndexingService                             |
|  2. Save image to MinIO (temp-images bucket)                      |
|  3. Create IndexedFile record in PostgreSQL (partial - no EXIF)   |
|  4. Publish FileDiscoveredMessage to RabbitMQ                     |
|  5. Return success to IndexingService                             |
|                                                                   |
|  CONSUMER FLOW (receives processing results):                     |
|  - MetadataExtractedMessage -> UPDATE IndexedFile with EXIF       |
|  - ThumbnailGeneratedMessage -> UPDATE IndexedFile.ThumbnailPath  |
+------------------------------------------------------------------+
    |
    v RabbitMQ (fan-out exchange)
    |
    +-----------------------------+-----------------------------+
    v                             v                             |
+------------------+      +------------------+                  |
| MetadataService  |      | ThumbnailService |                  |
| (NO DB ACCESS)   |      | (NO DB ACCESS)   |                  |
|                  |      |                  |                  |
| - Download from  |      | - Download from  |                  |
|   MinIO          |      |   MinIO          |                  |
| - Extract EXIF   |      | - Generate       |                  |
| - Publish        |      |   thumbnail      |                  |
|   metadata.*     |      | - Upload to      |                  |
|   (to RabbitMQ)  |      |   MinIO          |                  |
+--------+---------+      |   (thumbnails/)  |                  |
         |                | - Publish        |                  |
         |                |   thumbnail.*    |                  |
         |                +--------+---------+                  |
         |                         |                            |
         +-------------------------+----------------------------+
                                   |
                                   v (messages back to API)
                           API updates PostgreSQL

FRONTEND MEDIA ACCESS (Direct - no API relay):
+------------------------------------------------------------------+
| Frontend (Angular)                                                |
|                                                                   |
|  Traefik routes /thumbnails/* directly to MinIO                   |
|  <img src="/thumbnails/{fileHash}.jpg">                           |
+------------------------------------------------------------------+
         |
         v Direct HTTP to MinIO (NOT through API)
    +------------------+
    |     MinIO        |
    |  thumbnails/     |  <- Public read bucket
    |  bucket          |
    +------------------+
```

### Key Design Principles

1. **API is SOLE DB owner** - MetadataService/ThumbnailService never touch the database
2. **Processing services are stateless** - consume message, process, publish result
3. **Frontend accesses media DIRECTLY** from MinIO (not via API relay)
4. **IndexingService has ONE dependency** - just HTTP to API (existing pattern)

## Technology Stack

| Component | Technology | License | Purpose |
|-----------|------------|---------|---------|
| Message Bus | RabbitMQ | MPL 2.0 | Async message routing |
| .NET Client | MassTransit | Apache 2.0 | Message abstractions, retry, DLQ |
| Object Storage | MinIO | Apache 2.0 | S3-compatible image storage |
| Image Processing | ImageSharp | Apache 2.0 | EXIF extraction, thumbnail generation |

## Message Contracts

```csharp
// src/Shared/Messages/FileDiscoveredMessage.cs
public record FileDiscoveredMessage
{
    public Guid CorrelationId { get; init; }
    public Guid IndexedFileId { get; init; }
    public Guid ScanDirectoryId { get; init; }
    public string FilePath { get; init; }
    public string FileName { get; init; }
    public string FileHash { get; init; }
    public long FileSize { get; init; }
    public DateTime ModifiedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public string ObjectKey { get; init; }  // MinIO temp-images bucket key
}

// src/Shared/Messages/MetadataExtractedMessage.cs
public record MetadataExtractedMessage
{
    public Guid CorrelationId { get; init; }
    public Guid IndexedFileId { get; init; }
    public string FileHash { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public DateTime? DateTaken { get; init; }
    public string? CameraMake { get; init; }
    public string? CameraModel { get; init; }
    public double? GpsLatitude { get; init; }
    public double? GpsLongitude { get; init; }
    public int? Iso { get; init; }
    public string? Aperture { get; init; }
    public string? ShutterSpeed { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
}

// src/Shared/Messages/ThumbnailGeneratedMessage.cs
public record ThumbnailGeneratedMessage
{
    public Guid CorrelationId { get; init; }
    public Guid IndexedFileId { get; init; }
    public string FileHash { get; init; }
    public string ThumbnailPath { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public long SizeBytes { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
}

// src/Shared/Messages/ProcessingFailedMessage.cs
public record ProcessingFailedMessage
{
    public Guid CorrelationId { get; init; }
    public Guid IndexedFileId { get; init; }
    public string FileHash { get; init; }
    public string JobType { get; init; }  // "metadata" | "thumbnail"
    public string Error { get; init; }
    public int RetryCount { get; init; }
}
```

## New Projects

### MetadataService

```
src/MetadataService/
├── MetadataService.csproj
├── Program.cs
├── Consumers/
│   └── FileDiscoveredConsumer.cs
├── Services/
│   ├── IMetadataExtractor.cs
│   └── MetadataExtractor.cs  (ImageSharp-based)
├── appsettings.json
└── Dockerfile
```

**Configuration:**
```env
MESSAGE_BUS_CONNECTION=amqp://rabbitmq:5672
MINIO_ENDPOINT=minio:9000
MINIO_ACCESS_KEY=minioadmin
MINIO_SECRET_KEY=minioadmin
MAX_PARALLELISM=4
```

### ThumbnailService

```
src/ThumbnailService/
├── ThumbnailService.csproj
├── Program.cs
├── Consumers/
│   └── FileDiscoveredConsumer.cs
├── Services/
│   ├── IThumbnailGenerator.cs
│   └── ThumbnailGenerator.cs  (ImageSharp-based)
├── appsettings.json
└── Dockerfile
```

**Configuration:**
```env
MESSAGE_BUS_CONNECTION=amqp://rabbitmq:5672
MINIO_ENDPOINT=minio:9000
MINIO_ACCESS_KEY=minioadmin
MINIO_SECRET_KEY=minioadmin
THUMBNAIL_BUCKET=thumbnails
THUMBNAIL_MAX_WIDTH=200
THUMBNAIL_MAX_HEIGHT=200
THUMBNAIL_QUALITY=80
MAX_PARALLELISM=8
```

## Infrastructure Components

### RabbitMQ

```yaml
# docker-compose.yml
rabbitmq:
  image: rabbitmq:3-management-alpine
  container_name: photos-index-rabbitmq
  ports:
    - "5672:5672"   # AMQP
    - "15672:15672" # Management UI
  environment:
    RABBITMQ_DEFAULT_USER: photos
    RABBITMQ_DEFAULT_PASS: photos
  volumes:
    - rabbitmq_data:/var/lib/rabbitmq
  healthcheck:
    test: rabbitmq-diagnostics -q ping
    interval: 10s
    timeout: 5s
    retries: 5
```

### MinIO

```yaml
# docker-compose.yml
minio:
  image: minio/minio:latest
  command: server /data --console-address ":9001"
  environment:
    MINIO_ROOT_USER: minioadmin
    MINIO_ROOT_PASSWORD: minioadmin
  ports:
    - "9000:9000"   # API
    - "9001:9001"   # Console UI
  volumes:
    - minio_data:/data
  healthcheck:
    test: ["CMD", "curl", "-f", "http://localhost:9000/minio/health/live"]
```

**Buckets:**
- `temp-images` - Incoming images (auto-expire after 24h)
- `thumbnails` - Generated thumbnails (permanent, public read)

### Traefik Routing for Thumbnails

```yaml
# Direct routing to MinIO for thumbnail access
traefik:
  labels:
    - "traefik.http.routers.thumbnails.rule=PathPrefix(`/thumbnails`)"
    - "traefik.http.routers.thumbnails.service=minio"
    - "traefik.http.services.minio.loadbalancer.server.port=9000"
```

## Implementation Phases

### Phase 1: Infrastructure Setup
- [ ] Add RabbitMQ container to docker-compose
- [ ] Add MinIO container to docker-compose
- [ ] Create `src/Shared/Messages/` contracts
- [ ] Create `src/Shared/Storage/` MinIO client abstraction
- [ ] Add NuGet packages: MassTransit.RabbitMQ, AWSSDK.S3
- [ ] Update Directory.Packages.props

### Phase 2: API Gateway Enhancement
- [ ] Add MinIO client to API for saving incoming images
- [ ] Add MassTransit publisher to API
- [ ] Create/modify `POST /api/files/ingest` endpoint (multipart)
- [ ] Add MassTransit consumers:
  - MetadataExtractedConsumer -> update IndexedFile with EXIF
  - ThumbnailGeneratedConsumer -> update ThumbnailPath
- [ ] Add `/api/processing/status` endpoint
- [ ] Add health checks for RabbitMQ, MinIO

### Phase 3: MetadataService
- [ ] Create `src/MetadataService/` project
- [ ] Implement FileDiscoveredConsumer
- [ ] Create MetadataExtractor (ImageSharp-based)
- [ ] Add MinIO client for downloading images
- [ ] Add Dockerfile
- [ ] Add unit tests (85% coverage)

### Phase 4: ThumbnailService
- [ ] Create `src/ThumbnailService/` project
- [ ] Implement FileDiscoveredConsumer
- [ ] Create ThumbnailGenerator (ImageSharp-based)
- [ ] Add MinIO client for download/upload thumbnails
- [ ] Add Dockerfile
- [ ] Add unit tests (85% coverage)

### Phase 5: IndexingService Simplification
- [ ] Modify batch endpoint to send image bytes (multipart)
- [ ] Remove local metadata extraction (if exists)
- [ ] Remove local thumbnail generation (if exists)
- [ ] **NO new dependencies** - just HTTP to API (existing pattern)

### Phase 6: Deployment & Observability
- [ ] Update docker-compose.yml with all services
- [ ] Update kubernetes manifest
- [ ] Add OpenTelemetry to new services
- [ ] Configure correlation ID propagation
- [ ] Add Traefik route for MinIO thumbnails
- [ ] Add RabbitMQ management UI access (port 15672)
- [ ] Add MinIO console UI access (port 9001)

## Files to Create/Modify

### New Files
```
src/MetadataService/                     # New project
src/ThumbnailService/                    # New project
src/Shared/Messages/                     # Message contracts
  ├── FileDiscoveredMessage.cs
  ├── MetadataExtractedMessage.cs
  ├── ThumbnailGeneratedMessage.cs
  └── ProcessingFailedMessage.cs
src/Shared/Storage/                      # Storage abstractions
  ├── IObjectStorage.cs
  ├── MinioStorageClient.cs
  └── StorageOptions.cs
src/Api/Consumers/                       # API message consumers
  ├── MetadataExtractedConsumer.cs
  └── ThumbnailGeneratedConsumer.cs
src/Api/Controllers/ProcessingController.cs  # Status endpoint
```

### Modified Files
```
src/IndexingService/Services/PhotosApiClient.cs  # Multipart support
src/Api/Program.cs                               # MassTransit, MinIO DI
src/Api/Controllers/FilesController.cs           # Multipart ingest
deploy/docker/docker-compose.yml                 # New services
deploy/kubernetes/photos-index.yaml              # New containers
Directory.Packages.props                         # New packages
```

### NuGet Packages to Add
```xml
<PackageVersion Include="MassTransit" Version="8.2.0" />
<PackageVersion Include="MassTransit.RabbitMQ" Version="8.2.0" />
<PackageVersion Include="AWSSDK.S3" Version="3.7.305" />
```

## Monitoring & Observability

### Metrics (OpenTelemetry)

| Metric | Type | Description |
|--------|------|-------------|
| `processing_queue_depth` | Gauge | Current pending messages |
| `metadata_extraction_duration_ms` | Histogram | EXIF extraction time |
| `thumbnail_generation_duration_ms` | Histogram | Thumbnail creation time |
| `processing_success_total` | Counter | Successful processing |
| `processing_failed_total` | Counter | Failed processing |

### Health Checks
- RabbitMQ connection health
- MinIO connection health
- Queue depth thresholds
- Processing latency thresholds

### Dashboard Access
- RabbitMQ Management: http://localhost:15672
- MinIO Console: http://localhost:9001
- Aspire Dashboard: http://localhost:18888

## Configuration

### IndexingService (Synology)
```env
# Send images to API (existing pattern, now with multipart)
API_ENDPOINT=http://api:5000
BATCH_SIZE=50

# Disable local processing (offloaded to MPC)
GENERATE_THUMBNAILS=false
EXTRACT_METADATA=false
```

### API (MPC)
```env
# MinIO configuration
MINIO_ENDPOINT=minio:9000
MINIO_ACCESS_KEY=minioadmin
MINIO_SECRET_KEY=minioadmin
MINIO_TEMP_BUCKET=temp-images
MINIO_THUMBNAIL_BUCKET=thumbnails

# RabbitMQ configuration
RABBITMQ_HOST=rabbitmq
RABBITMQ_USER=photos
RABBITMQ_PASSWORD=photos
```

## Success Criteria

- [ ] Thumbnails generated without impacting Synology CPU
- [ ] Metadata extraction offloaded to MPC
- [ ] Processing rate: >10 items/second with parallelism
- [ ] Zero data loss on service restarts
- [ ] Correlation ID preserved across message flow
- [ ] Observability in Aspire dashboard
- [ ] RabbitMQ dead letter queue for failed messages
- [ ] MinIO lifecycle policy for temp image cleanup

## Future Enhancements

### Phase 2 - Authentication
- Presigned URLs for thumbnail access (instead of public bucket)
- API generates short-lived URLs for authenticated users

### Cloud Migration Path
- MinIO -> AWS S3 / Azure Blob Storage
- RabbitMQ -> Azure Service Bus / AWS SQS
- Same message contracts, different transport

## References

- [ADR-004: Distributed Processing Architecture](../../adrs/004-distributed-processing-architecture.md)
- [MassTransit Documentation](https://masstransit.io/)
- [MinIO .NET SDK](https://min.io/docs/minio/linux/developers/dotnet/minio-dotnet.html)
- [RabbitMQ .NET Tutorial](https://www.rabbitmq.com/tutorials/tutorial-one-dotnet.html)
