# v0.3.0 Release Plan

## Summary
Major release with user-facing features and architectural improvement for thumbnail/metadata offloading to the API (MPC).

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

### 4. Thumbnail & Metadata Offload to API (`09-thumbnail-offload`)
**Branch:** `feature/thumbnail-offload`

This is the major architectural change - offload compute-intensive work from Synology (IndexingService) to MPC (API).

**Phase A: Database & Models**
- `src/Database/Entities/ThumbnailJob.cs` (new entity)
- EF Core migration for `ThumbnailJobs` table
- `src/Api/Repositories/IThumbnailJobRepository.cs`

**Phase B: API Changes**
- Modify `POST /api/files/batch` to accept multipart form data with image bytes
- Save temp images, create ThumbnailJob records
- Add `GET /api/thumbnails/status` endpoint

**Phase C: ThumbnailWorker (BackgroundService in API)**
- `src/Api/Workers/ThumbnailWorker.cs`
- `src/Api/Services/ThumbnailGenerator.cs` (ImageSharp)
- `src/Api/Services/ThumbnailRecoveryService.cs` (crash recovery)
- Configurable parallelism (default 4 workers)

**Phase D: IndexingService Changes**
- Add `OFFLOAD_PROCESSING=true` config option
- Read image bytes for new/changed files
- Send multipart batch with filesystem metadata + images
- **NO local EXIF extraction** - only filesystem data (path, size, dates)
- Hash computation stays local (needed for dedup before sending)

**Phase E: Monitoring**
- OpenTelemetry metrics for thumbnail queue
- Health check endpoint
- Aspire dashboard updates

## Implementation Order

1. **Hash Computer** (`03-002`) - independent, can be done first
2. **Dashboard** (`05-002`) - frontend, independent
3. **Directory Settings** (`05-003`) - frontend, independent
4. **Thumbnail Offload** (`09-thumbnail-offload`) - depends on hash computer being integrated

Tasks 1-3 can be done in parallel.

## Key Decisions

- **Multipart form** (not Base64) for image transfer - avoids 33% overhead
- **ThumbnailWorker inside API** (not separate service) - simpler for now, can extract later
- **PostgreSQL queue** (ThumbnailJobs table) - no RabbitMQ dependency yet
- **EXIF extraction on API side** - offload ImageSharp compute from Synology
- **Hash computation on IndexingService** - needed for dedup before sending to API

### Architecture Split
```
IndexingService (Synology - low CPU):
  ├── File scanning (I/O)
  ├── SHA256 hash computation (streaming)
  └── Read image bytes (I/O)

API (MPC - high CPU):
  ├── EXIF metadata extraction (ImageSharp)
  ├── Thumbnail generation (ImageSharp)
  └── Database storage
```

## Configuration Changes

### IndexingService
```env
OFFLOAD_THUMBNAILS=true
BATCH_SIZE=50
GENERATE_THUMBNAILS=false
```

### API
```env
THUMBNAIL_TEMP_DIR=/app/temp/images
THUMBNAIL_DIR=/app/thumbnails
THUMBNAIL_WORKER_PARALLELISM=4
THUMBNAIL_WORKER_BATCH_SIZE=10
```

## Test Coverage Requirements
- Hash Computer: 90%
- Dashboard: 80%
- Directory Settings: 80%
- ThumbnailWorker: 85%
- API batch endpoint: 90%

## Testing Strategy

### Unit Tests
| Component | Coverage | Key Tests |
|-----------|----------|-----------|
| HashComputer | 90% | Streaming, large files, cancellation, locked files |
| ThumbnailWorker | 85% | Job processing, retry logic, crash recovery |
| ThumbnailGenerator | 90% | EXIF extraction, resize, auto-orient, corrupted images |
| Dashboard | 80% | Stats display, loading states, error handling |
| Directory Settings | 80% | CRUD operations, form validation, dialogs |

### Integration Tests
- `tests/Integration.Tests/ThumbnailOffloadTests.cs`
  - Full flow: IndexingService → API batch → ThumbnailWorker → thumbnail file
  - Crash recovery scenarios (API restart mid-processing)
  - Concurrent batch processing

### E2E Tests (Playwright)
- Dashboard data display and refresh
- Directory add/edit/delete flow
- Thumbnail visibility in file views

### Performance Tests
- Hash computation: measure throughput for 1MB, 100MB, 1GB files
- Thumbnail processing: target >10/second with parallelism=4
- Network transfer: verify multipart efficiency vs Base64

## Documentation Updates

### User Documentation
- `docs/user-guide/dashboard.md` - Dashboard features and usage
- `docs/user-guide/directory-management.md` - Managing scan directories
- Update deployment docs with new config options

### Technical Documentation
- `docs/architecture/thumbnail-offload.md` - Architecture decision and flow
- Update `CLAUDE.md` with new services/components
- API documentation (Swagger) for new/modified endpoints

## Architecture Decision Records (ADRs)

### ADR Required
- **ADR-00X: Thumbnail Processing Offload**
  - Context: Synology NAS CPU limitations
  - Decision: Offload to API (MPC) via multipart batch
  - Consequences: Network dependency, but better resource utilization
  - Alternatives considered: On-demand generation, separate service, message queue

### ADR Consideration
- **ADR-00Y: PostgreSQL Queue vs Message Bus** (if needed)
  - Why ThumbnailJobs table instead of RabbitMQ
  - Trade-offs and migration path

## Backlog Updates

After implementation, update `docs/backlog/README.md`:
- Mark `03-002` Hash Computer as ✅ Complete
- Mark `05-002` Dashboard as ✅ Complete
- Mark `05-003` Directory Settings as ✅ Complete
- Mark `09-thumbnail-offload` as ✅ Complete
- Update task files with PR links

## Release Checklist

### Pre-release
- [ ] All features merged to main
- [ ] CI passing (unit, integration, E2E)
- [ ] Documentation updated
- [ ] ADR written and reviewed
- [ ] Backlog updated

### Release
- [ ] Create tag `v0.3.0`
- [ ] Verify container images published
- [ ] Update deployment examples with new config

### Post-release
- [ ] Monitor Aspire dashboard for thumbnail queue
- [ ] Verify Synology CPU usage reduced
- [ ] Collect performance metrics

## Success Criteria
- [ ] Dashboard shows stats, directories, quick actions
- [ ] Users can manage directories via UI
- [ ] Hash computation is streaming and memory-efficient
- [ ] Thumbnails generated on MPC without impacting Synology
- [ ] Processing rate: >10 thumbnails/second
- [ ] Zero data loss on crashes/restarts
- [ ] ADR documented and approved
- [ ] Test coverage targets met
