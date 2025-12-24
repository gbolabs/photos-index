# v0.3.0 Release Plan

## Summary

Major release introducing distributed processing architecture with event-driven microservices. Offloads compute-intensive operations (thumbnail generation, metadata extraction) from Synology NAS to MPC (TrueNAS).

**Related ADR**: [ADR-004: Distributed Processing Architecture](../../adrs/004-distributed-processing-architecture.md)

---

## Design Philosophy

This is a **home/personal project** running on NAS hardware. The architecture should be:

- **KISS**: Keep It Simple, Stupid
- **YAGNI**: You Aren't Gonna Need It
- **Functional > Perfect**: Working solution over perfect architecture
- **Maintainable > Scalable**: Easy to maintain over enterprise-scale

Enterprise patterns (PagerDuty alerts, TLS between internal containers, 90%+ test coverage, feature flags) are overkill and add unnecessary complexity.

---

## Already Implemented (Scope Reduction)

These features are **already done** on the branch:

- **Hash Computer (03-002)** - `7a8f146 Add HashComputer for streaming SHA256 computation`
- **Dashboard (05-002)** - `c7fe406 Implement SPA features: Dashboard...`
- **Directory Settings (05-003)** - Part of SPA features

---

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
    v                             v
+------------------+      +------------------+
| MetadataService  |      | ThumbnailService |
| (NO DB ACCESS)   |      | (NO DB ACCESS)   |
| - Extract EXIF   |      | - Generate thumb |
| - Publish result |      | - Upload MinIO   |
+------------------+      +------------------+
         |                         |
         +-------------------------+
                    |
                    v (messages back to API)
            API updates PostgreSQL

FRONTEND: Traefik routes /thumbnails/* directly to MinIO
```

### Key Design Principles

1. **API is SOLE DB owner** - Processing services never touch database
2. **Processing services are stateless** - Consume message, process, publish result
3. **IndexingService has ONE dependency** - Just HTTP to API (existing pattern)
4. **Frontend accesses media DIRECTLY from MinIO** - Not via API relay

### Technology Choices

| Component | Technology | License |
|-----------|------------|---------|
| Message Bus | RabbitMQ + MassTransit | MPL 2.0 / Apache 2.0 |
| Object Storage | MinIO | Apache 2.0 |

---

## Deployment Environments

| Environment | Technology | Purpose | Services |
|-------------|------------|---------|----------|
| **Local Dev** | Podman | Development & testing | All services |
| **Synology NAS** | Docker Compose | File indexing | IndexingService only |
| **TrueNAS MPC** | Docker Compose | Processing infrastructure | API, MetadataService, ThumbnailService, RabbitMQ, MinIO |

**Key Insight**: Split responsibility based on hardware capabilities:
- **Synology**: Lightweight operations (scan, hash, read bytes)
- **TrueNAS MPC**: Compute-intensive operations (thumbnails, metadata)

---

## Implementation Phases

### Phase 1: Infrastructure
1. Add RabbitMQ container to docker-compose
2. Add MinIO container to docker-compose
3. Create `src/Shared/Messages/` contracts
4. Create `src/Shared/Storage/` MinIO client abstraction
5. Add NuGet packages: MassTransit.RabbitMQ, Minio

### Phase 2: API Gateway
1. Modify `POST /api/files/ingest` for multipart form data
2. Add MinIO client for saving incoming images
3. Add MassTransit publisher for FileDiscoveredMessage
4. Add consumers for MetadataExtracted/ThumbnailGenerated messages
5. Add `/api/processing/status` endpoint

### Phase 3: Processing Services
1. Create `src/MetadataService/` project with FileDiscoveredConsumer
2. Create `src/ThumbnailService/` project with FileDiscoveredConsumer
3. Basic OpenTelemetry integration (already in project pattern)

### Phase 4: IndexingService Changes
1. Modify batch endpoint to send multipart with image bytes
2. Remove local metadata extraction
3. Remove local thumbnail generation

### Phase 5: UI Enhancements (User Value)
1. Display thumbnails in file list/grid view
2. Display extracted metadata in file details panel
3. Traefik route for MinIO thumbnail access

### Phase 6: Deployment
1. Update docker-compose.yml for all environments
2. Update kubernetes manifests
3. Configure Traefik routes

---

## Configuration

### IndexingService (Synology)
```env
API_ENDPOINT=http://truenas-mpc:5000
BATCH_SIZE=50
GENERATE_THUMBNAILS=false
EXTRACT_METADATA=false
```

### API (TrueNAS MPC)
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

### Processing Services
```env
MESSAGE_BUS_CONNECTION=amqp://rabbitmq:5672
MINIO_ENDPOINT=minio:9000
MAX_PARALLELISM=4  # MetadataService (start conservative)
MAX_PARALLELISM=8  # ThumbnailService
```

---

## Operational Approach

### Error Handling (Keep Simple)
- 3 retries with exponential backoff (MassTransit default)
- Dead Letter Queue for failed messages (automatic)
- Manual inspection via RabbitMQ Management UI

### Monitoring (What We Already Have)
- Aspire Dashboard for traces/logs
- RabbitMQ Management UI for queue visibility
- MinIO Console for storage inspection
- Docker logs for troubleshooting

### Security (Home Network Appropriate)
- MinIO public bucket for thumbnails (Phase 1)
- RabbitMQ non-default credentials
- Services on Docker internal network
- HTTPS at Traefik edge if exposing externally

**Skip** (overkill for home use):
- TLS between internal containers
- IAM policies for MinIO
- Circuit breakers
- PagerDuty/Slack alerts
- Prometheus/Grafana

---

## Success Criteria

### Must Have (Technical)
- [ ] Thumbnails generated on MPC (not Synology)
- [ ] Metadata extracted on MPC (not Synology)
- [ ] No data loss on service restarts
- [ ] Synology CPU usage noticeably reduced

### Must Have (User Value)
- [ ] **Thumbnails visible in UI** - Users see actual image previews
- [ ] **Metadata displayed in UI** - Users see EXIF data (date taken, camera, dimensions)
- [ ] Dashboard and directory settings working (already done)

### Skip Measuring
- ~~Processing rate > 10 items/sec~~ (arbitrary metric)
- ~~Test coverage percentages~~ (test what matters)
- ~~Formal documentation~~ (keep it simple)

---

## Release Checklist

### Pre-release
- [ ] All features merged to main
- [ ] CI passing (unit, integration, E2E)
- [ ] ADR-004 documented

### Release
- [ ] Create tag `v0.3.0`
- [ ] Verify container images published:
  - `ghcr.io/gbolabs/photos-index/api:v0.3.0`
  - `ghcr.io/gbolabs/photos-index/web:v0.3.0`
  - `ghcr.io/gbolabs/photos-index/indexing-service:v0.3.0`
  - `ghcr.io/gbolabs/photos-index/metadata-service:v0.3.0`
  - `ghcr.io/gbolabs/photos-index/thumbnail-service:v0.3.0`

### Post-release
- [ ] Verify message flow in RabbitMQ UI
- [ ] Verify thumbnails accessible in UI
- [ ] Verify Synology CPU usage reduced

---

## Files to Create/Modify

**New Services**:
- `src/MetadataService/`
- `src/ThumbnailService/`
- `src/Shared/Messages/`
- `src/Shared/Storage/`

**API Changes**:
- `src/Api/Controllers/` - Multipart ingest, processing status
- `src/Api/Services/` - MassTransit integration

**UI Enhancements**:
- `src/Web/src/app/features/files/` - Thumbnail display, metadata panel

**Deployment**:
- `deploy/docker/docker-compose.yml`
- `deploy/docker/docker-compose.synology.yml`
- `deploy/kubernetes/photos-index.yaml`

---

## Rollback Plan

If distributed processing fails:
1. Stop processing services
2. Re-enable local processing in IndexingService (`GENERATE_THUMBNAILS=true`, `EXTRACT_METADATA=true`)
3. That's it. No feature flags needed.
