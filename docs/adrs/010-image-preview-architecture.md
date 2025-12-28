# ADR-010: Image Preview Architecture

## Status
Accepted

## Date
2024-12-28

## Context

The Photos Index application displays thumbnails in the file browser, but users need the ability to preview full-resolution images. The challenge is that:

1. **Distributed Architecture**: Files are stored on Synology NAS, but the API runs on TrueNAS
2. **No Direct Access**: The API cannot directly serve files from the Synology NAS
3. **User Experience**: Users expect instant preview when clicking the eye icon

## Decision

We implement an on-demand preview system where:

1. **API triggers preview request** via SignalR to connected indexers
2. **Indexer uploads the file** to MinIO `previews` bucket (the indexer has access to the files)
3. **MinIO serves the preview** via Traefik routing
4. **Auto-expiration**: MinIO lifecycle policy deletes previews after 1 day

### Architecture

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Browser   │     │     API     │     │   Indexer   │     │    MinIO    │
│             │     │  (TrueNAS)  │     │  (Synology) │     │  (TrueNAS)  │
└──────┬──────┘     └──────┬──────┘     └──────┬──────┘     └──────┬──────┘
       │                   │                   │                   │
       │ 1. POST /api/files/{id}/preview       │                   │
       │──────────────────>│                   │                   │
       │                   │                   │                   │
       │                   │ 2. SignalR: RequestPreview            │
       │                   │──────────────────>│                   │
       │                   │                   │                   │
       │                   │                   │ 3. Read file      │
       │                   │                   │ from disk         │
       │                   │                   │                   │
       │                   │                   │ 4. Upload to MinIO│
       │                   │                   │──────────────────>│
       │                   │                   │                   │
       │                   │ 5. SignalR: PreviewReady (URL)        │
       │<──────────────────│<──────────────────│                   │
       │                   │                   │                   │
       │ 6. GET /previews/{key}                │                   │
       │───────────────────────────────────────────────────────────>│
       │                   │                   │                   │
       │<───────────────────────────────────────────────────────────│
       │ 7. Image content                      │                   │
```

### MinIO Previews Bucket

- **Bucket name**: `previews`
- **Public read**: Enabled (served via Traefik)
- **Lifecycle policy**: Objects expire after 1 day
- **Object key pattern**: `preview-{fileId}.{ext}`

### SignalR Messages

**API → Indexer:**
- `RequestPreview(Guid fileId, string filePath)`: Request preview generation

**Indexer → API (Hub methods):**
- `ReportPreviewReady(Guid fileId, string previewUrl)`: Preview uploaded successfully
- `ReportPreviewFailed(Guid fileId, string error)`: Preview generation failed

### Angular UI

- **Blur effect**: Shows blurred thumbnail with spinner while loading
- **Smooth transition**: Fades in full-resolution image when ready
- **Error handling**: Shows error message if preview fails

## Alternatives Considered

### Option A: Presigned URLs
Generate presigned MinIO URLs for existing thumbnails. Rejected because thumbnails are not stored on Synology NAS.

### Option B: Streaming through API
Stream file content through API. Rejected because API doesn't have file access and would require complex proxying.

### Option C: NFS/SMB mounting
Mount Synology shares on TrueNAS. Rejected due to complexity and performance concerns.

## Consequences

### Positive
- Works with distributed architecture (files on Synology, API on TrueNAS)
- Leverages existing SignalR infrastructure
- Auto-cleanup via MinIO lifecycle (no maintenance job needed)
- Good UX with blur→full transition

### Negative
- Preview generation takes time (file upload to MinIO)
- Requires indexer to be connected and have file access
- Storage overhead for preview copies (mitigated by 1-day expiration)

## Implementation

### Files Modified

**Backend:**
- `src/Api/Hubs/IndexerHub.cs`: Added `RequestPreview`, `ReportPreviewReady`, `ReportPreviewFailed`
- `src/Api/Controllers/IndexedFilesController.cs`: Added `POST /api/files/{id}/preview`
- `src/Api/Program.cs`: Added previews bucket initialization with lifecycle
- `src/IndexingService/Services/SignalRClientService.cs`: Added preview handler
- `src/IndexingService/Program.cs`: Added MinIO configuration

**Frontend:**
- `src/Web/src/app/services/signalr.service.ts`: New SignalR service
- `src/Web/src/app/shared/components/image-preview-modal/`: New preview modal component
- `src/Web/src/app/features/files/files.ts`: Added preview, navigation, and filter handlers

**Infrastructure:**
- `deploy/docker/docker-compose.yml`: Added Traefik routing for `/previews`

## Related Issues
- GitHub Issue #125: File view (list-view) enhancements
