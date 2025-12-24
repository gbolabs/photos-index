# ADR-004: Distributed Processing Architecture

**Status**: Proposed
**Date**: 2025-12-24
**Author**: Claude Code

## Context

The current photos-index architecture has a significant performance bottleneck: the Synology NAS running the IndexingService has limited CPU resources (Intel Celeron J3455), while compute-intensive operations like thumbnail generation and EXIF metadata extraction require substantial processing power.

Current architecture:
- IndexingService performs all work locally (scanning, hashing, metadata extraction, thumbnails)
- Synchronous HTTP communication to API for data ingestion
- No message bus or async infrastructure
- Thumbnails currently disabled due to Synology performance impact

The MPC (TrueNAS) has significantly more resources:
- AMD Ryzen 5 5500U (6 cores / 12 threads)
- 32GB RAM (15GB free)
- NVMe/SSD storage

We need to offload compute-intensive work from the Synology NAS to the MPC while maintaining a clean architecture.

## Decision

Implement a distributed, event-driven architecture with:

1. **RabbitMQ + MassTransit** for asynchronous messaging
2. **MinIO** for object storage (S3-compatible)
3. **Dedicated processing services** (MetadataService, ThumbnailService)
4. **API as sole database owner and gateway**

### Architecture Overview

```
IndexingService (Synology - MINIMAL responsibilities)
    |
    | HTTP POST /api/files/ingest (multipart: metadata + image bytes)
    | (Single communication channel - keeps existing pattern)
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
```

### Key Principles

1. **API is SOLE DB owner** - MetadataService/ThumbnailService never touch the database
2. **Processing services are stateless** - consume message, process, publish result
3. **Frontend accesses media DIRECTLY** from MinIO (not via API relay)
4. **IndexingService has ONE dependency** - just HTTP to API (existing pattern)

### Technology Choices

| Component | Technology | License | Rationale |
|-----------|------------|---------|-----------|
| Message Bus | RabbitMQ | MPL 2.0 | Battle-tested, rich routing, management UI |
| .NET Client | MassTransit | Apache 2.0 | Robust abstractions, retry, dead letter |
| Object Storage | MinIO | Apache 2.0 | S3-compatible, single container, production-ready |

### Frontend Thumbnail Access

**Phase 1 (Current Implementation):** Traefik direct route to MinIO
- MinIO `thumbnails` bucket set to public read
- Frontend uses `<img src="/thumbnails/{fileHash}.jpg">`
- Zero API involvement, Traefik handles routing

**Phase 2 (Future with Auth):** Presigned URLs
- API generates short-lived URLs for authenticated users
- Frontend fetches URL from API, then loads from MinIO

### Message Contracts

```csharp
// FileDiscoveredMessage - Published by API after receiving file
public record FileDiscoveredMessage
{
    public Guid CorrelationId { get; init; }
    public Guid IndexedFileId { get; init; }
    public Guid ScanDirectoryId { get; init; }
    public string FilePath { get; init; }
    public string FileHash { get; init; }
    public long FileSize { get; init; }
    public string ObjectKey { get; init; }  // MinIO object key
}

// MetadataExtractedMessage - Published by MetadataService
public record MetadataExtractedMessage
{
    public Guid CorrelationId { get; init; }
    public Guid IndexedFileId { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public DateTime? DateTaken { get; init; }
    public string? CameraMake { get; init; }
    public string? CameraModel { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
}

// ThumbnailGeneratedMessage - Published by ThumbnailService
public record ThumbnailGeneratedMessage
{
    public Guid CorrelationId { get; init; }
    public Guid IndexedFileId { get; init; }
    public string ThumbnailPath { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
}
```

## Consequences

### Positive

- **Synology CPU freed** - Only lightweight operations (scan, hash, read bytes)
- **MPC resources utilized** - Dedicated processing with parallel workers
- **Horizontal scaling** - Can run multiple MetadataService/ThumbnailService instances
- **Fault isolation** - Processing service failure doesn't impact API
- **Better observability** - Queue depth visibility, message tracing
- **Decoupled evolution** - Services can be updated independently
- **Future-proof** - Can migrate to cloud (S3, Azure Service Bus) if needed

### Negative

- **Additional infrastructure** - RabbitMQ, MinIO containers
- **Increased complexity** - More moving parts to monitor
- **Network dependency** - Processing requires network between services
- **Learning curve** - Team needs to understand MassTransit/RabbitMQ concepts
- **Eventual consistency** - File visible in DB before metadata/thumbnail ready

### Neutral

- **No code changes needed in IndexingService** (other than multipart support)
- **Existing HTTP pattern preserved** - Just adds async processing behind the scenes

## Alternatives Considered

### 1. PostgreSQL-based Queue

Using a `processing_jobs` table with polling instead of RabbitMQ.

**Pros:** No additional infrastructure
**Cons:** Not designed for high-throughput messaging, polling adds latency

**Rejected:** We want push-based processing and proper message bus semantics.

### 2. Redis Streams

**Pros:** Very fast, simpler than RabbitMQ
**Cons:** Less feature-rich, no built-in dead letter queue, persistence requires configuration

**Rejected:** RabbitMQ provides better reliability guarantees.

### 3. Shared Volume Instead of MinIO

Using Docker volumes mounted to multiple containers.

**Pros:** Simpler, no additional container
**Cons:** Doesn't scale across hosts, no object lifecycle management

**Rejected:** MinIO provides S3 compatibility and scales independently.

### 4. Processing Inside API Container

Running thumbnail/metadata workers as BackgroundServices in API.

**Pros:** Simpler deployment, no additional containers
**Cons:** Competes for resources with API, can't scale independently

**Rejected:** Clean separation allows independent scaling and resource limits.

## Implementation Phases

### Phase 1: Infrastructure Setup
- Add RabbitMQ and MinIO containers to docker-compose
- Create shared message contracts in `src/Shared/Messages/`
- Add MinIO client abstraction in `src/Shared/Storage/`
- Add NuGet packages (MassTransit.RabbitMQ, AWSSDK.S3)

### Phase 2: API Gateway Enhancement
- Modify `POST /api/files/ingest` for multipart with image bytes
- Add MinIO client for saving incoming images
- Add MassTransit publisher for FileDiscoveredMessage
- Add consumers for MetadataExtractedMessage and ThumbnailGeneratedMessage

### Phase 3: MetadataService
- Create new `src/MetadataService/` project
- Implement FileDiscoveredConsumer
- Create MetadataExtractor (ImageSharp-based)

### Phase 4: ThumbnailService
- Create new `src/ThumbnailService/` project
- Implement FileDiscoveredConsumer
- Create ThumbnailGenerator (ImageSharp-based)

### Phase 5: IndexingService Simplification
- Add multipart support for image byte transfer
- Remove any local metadata/thumbnail processing

### Phase 6: Deployment & Observability
- Update docker-compose and kubernetes manifests
- Add OpenTelemetry to new services
- Configure Traefik routes for MinIO thumbnails

## References

- [MassTransit Documentation](https://masstransit.io/)
- [MinIO .NET SDK](https://min.io/docs/minio/linux/developers/dotnet/minio-dotnet.html)
- [RabbitMQ Management](https://www.rabbitmq.com/management.html)
- Related backlog: `docs/backlog/09-thumbnail-offload/README.md`
- Related backlog: `docs/backlog/09-fixes/003-service-bus-communication.md`
