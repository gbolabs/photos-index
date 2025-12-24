# v0.3.0 Release Plan

## Summary

Major release introducing distributed processing architecture with event-driven microservices. Offloads compute-intensive operations (thumbnail generation, metadata extraction) from Synology NAS to MPC (TrueNAS).

**Related ADR**: [ADR-004: Distributed Processing Architecture](../../adrs/004-distributed-processing-architecture.md)

## Features to Bundle

### 1. Dashboard (`05-002`)
**Branch:** `feature/web-dashboard`
**Files:**
- `src/Web/src/app/features/dashboard/` (new components)
- Stats cards, directory status, quick actions
- Auto-refresh, loading skeletons, error handling

### 2. Directory Settings UI (`05-003`)
**Branch:** `feature/web-directory-settings`
**Files:**
- `src/Web/src/app/features/settings/` (enhance existing)
- Add/edit/delete directories via dialogs
- Toggle enable/disable, trigger scans

### 3. Hash Computer (`03-002`)
**Branch:** `feature/indexing-hash-computer`
**Files:**
- `src/IndexingService/Services/IHashComputer.cs`
- `src/IndexingService/Services/HashComputer.cs`
- `src/IndexingService/Models/HashResult.cs`
- Streaming SHA256, progress reporting, parallel batch processing

### 4. Distributed Processing Architecture (`09-thumbnail-offload`)
**Branch:** `feature/distributed-processing`

This is the major architectural change - offload compute-intensive work from Synology (IndexingService) to MPC using event-driven microservices.

**New Infrastructure:**
- RabbitMQ (message bus)
- MinIO (object storage)

**New Services:**
- `src/MetadataService/` - EXIF extraction service
- `src/ThumbnailService/` - Thumbnail generation service

**Phase A: Infrastructure Setup**
- Add RabbitMQ container to docker-compose
- Add MinIO container to docker-compose
- Create `src/Shared/Messages/` contracts
- Create `src/Shared/Storage/` MinIO client abstraction
- Add NuGet packages: MassTransit.RabbitMQ, AWSSDK.S3

**Phase B: API Gateway Enhancement**
- Modify `POST /api/files/ingest` for multipart form data
- Add MinIO client for saving incoming images
- Add MassTransit publisher for FileDiscoveredMessage
- Add consumers for MetadataExtracted/ThumbnailGenerated messages
- Add `/api/processing/status` endpoint

**Phase C: MetadataService**
- Create `src/MetadataService/` project
- Implement FileDiscoveredConsumer
- Create MetadataExtractor (ImageSharp-based)
- Add MinIO client for downloading images

**Phase D: ThumbnailService**
- Create `src/ThumbnailService/` project
- Implement FileDiscoveredConsumer
- Create ThumbnailGenerator (ImageSharp-based)
- Add MinIO client for download/upload

**Phase E: IndexingService Simplification**
- Modify batch endpoint to send multipart with image bytes
- Remove local metadata extraction
- Remove local thumbnail generation
- **NO new dependencies** - just HTTP to API

**Phase F: Deployment & Observability**
- Update docker-compose.yml and kubernetes manifest
- Add OpenTelemetry to new services
- Add Traefik route for MinIO thumbnails
- Configure RabbitMQ/MinIO console access

## Architecture Overview

```
IndexingService (Synology - MINIMAL responsibilities)
    |
    | HTTP POST /api/files/ingest (multipart: metadata + image bytes)
    v
+------------------------------------------------------------------+
| API Service (Gateway + SOLE DB OWNER)                             |
|  1. Save image to MinIO (temp-images bucket)                      |
|  2. Create IndexedFile record in PostgreSQL                       |
|  3. Publish FileDiscoveredMessage to RabbitMQ                     |
+------------------------------------------------------------------+
    |
    v RabbitMQ (fan-out exchange)
    |
    +-----------------------------+-----------------------------+
    v                             v                             |
+------------------+      +------------------+                  |
| MetadataService  |      | ThumbnailService |                  |
| (NO DB ACCESS)   |      | (NO DB ACCESS)   |                  |
| - Extract EXIF   |      | - Generate thumb |                  |
| - Publish result |      | - Upload MinIO   |                  |
+------------------+      +------------------+                  |
         |                         |                            |
         +-------------------------+----------------------------+
                                   |
                                   v (messages back to API)
                           API updates PostgreSQL

FRONTEND: Traefik routes /thumbnails/* directly to MinIO
```

## Key Design Decisions

- **Message Bus:** RabbitMQ + MassTransit (Apache 2.0)
- **Object Storage:** MinIO (Apache 2.0)
- **API is SOLE DB owner:** Processing services never touch database
- **Frontend thumbnail access:** Traefik direct to MinIO (Phase 1), Presigned URLs (Phase 2 with auth)
- **IndexingService dependency:** HTTP to API only (no message bus client)

## Implementation Order

1. **Hash Computer** (`03-002`) - independent, can be done first
2. **Dashboard** (`05-002`) - frontend, independent
3. **Directory Settings** (`05-003`) - frontend, independent
4. **Infrastructure** - RabbitMQ, MinIO containers
5. **API Gateway** - multipart ingest, MassTransit
6. **MetadataService** - new container
7. **ThumbnailService** - new container
8. **IndexingService changes** - multipart support

Tasks 1-3 can be done in parallel. Tasks 4-8 are sequential.

## Test Coverage Requirements

| Component | Coverage |
|-----------|----------|
| Hash Computer | 90% |
| Dashboard | 80% |
| Directory Settings | 80% |
| MetadataService | 85% |
| ThumbnailService | 85% |
| API ingest/consumers | 90% |

## Configuration Changes

### IndexingService (Synology)
```env
API_ENDPOINT=http://api:5000
BATCH_SIZE=50
GENERATE_THUMBNAILS=false
EXTRACT_METADATA=false
```

### API (MPC)
```env
# MinIO
MINIO_ENDPOINT=minio:9000
MINIO_ACCESS_KEY=minioadmin
MINIO_SECRET_KEY=minioadmin
MINIO_TEMP_BUCKET=temp-images
MINIO_THUMBNAIL_BUCKET=thumbnails

# RabbitMQ
RABBITMQ_HOST=rabbitmq
RABBITMQ_USER=photos
RABBITMQ_PASSWORD=photos
```

### New Services
```env
MESSAGE_BUS_CONNECTION=amqp://rabbitmq:5672
MINIO_ENDPOINT=minio:9000
MAX_PARALLELISM=4  # MetadataService
MAX_PARALLELISM=8  # ThumbnailService
```

## Documentation Updates

### Required
- `docs/adrs/004-distributed-processing-architecture.md` - Architecture decision
- `docs/backlog/09-thumbnail-offload/README.md` - Updated with full architecture
- Update `CLAUDE.md` with new services/components
- API documentation (Swagger) for new endpoints

### User Documentation
- `docs/user-guide/dashboard.md` - Dashboard features
- `docs/user-guide/directory-management.md` - Managing directories
- Update deployment docs with new config options

## Backlog Updates

After implementation, update `docs/backlog/README.md`:
- Mark `03-002` Hash Computer as Complete
- Mark `05-002` Dashboard as Complete
- Mark `05-003` Directory Settings as Complete
- Mark `09-thumbnail-offload` as Complete
- Mark `09-003` Service Bus as Merged (superseded by 09-thumbnail-offload)

## Release Checklist

### Pre-release
- [ ] All features merged to main
- [ ] CI passing (unit, integration, E2E)
- [ ] ADR-004 written and approved
- [ ] Documentation updated
- [ ] Backlog updated

### Release
- [ ] Create tag `v0.3.0`
- [ ] Verify container images published:
  - `ghcr.io/gbolabs/photos-index/api:v0.3.0`
  - `ghcr.io/gbolabs/photos-index/web:v0.3.0`
  - `ghcr.io/gbolabs/photos-index/indexing-service:v0.3.0`
  - `ghcr.io/gbolabs/photos-index/metadata-service:v0.3.0`
  - `ghcr.io/gbolabs/photos-index/thumbnail-service:v0.3.0`
- [ ] Update deployment examples with new services

### Post-release
- [ ] Monitor RabbitMQ management UI for message flow
- [ ] Monitor MinIO console for storage usage
- [ ] Verify Synology CPU usage reduced
- [ ] Collect processing rate metrics (target: >10/sec)

## Success Criteria

- [ ] Dashboard shows stats, directories, quick actions
- [ ] Users can manage directories via UI
- [ ] Hash computation is streaming and memory-efficient
- [ ] Thumbnails generated on MPC without impacting Synology
- [ ] Metadata extraction on MPC without impacting Synology
- [ ] Processing rate: >10 items/second with parallelism
- [ ] Zero data loss on service restarts
- [ ] ADR-004 documented and approved
- [ ] Test coverage targets met
