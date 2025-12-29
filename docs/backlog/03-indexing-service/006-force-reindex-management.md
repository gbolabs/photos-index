# Force Reindex Management API

**Status**: 🔲 Not Started
**Priority**: Low
**Effort**: Medium

## Overview

Add API endpoints and UI controls to allow users to force files or directories to be rescanned, bypassing the incremental indexing optimization.

## Use Cases

1. **Metadata corruption** - Force re-extraction of metadata for specific files
2. **Thumbnail regeneration** - Force new thumbnails to be generated
3. **Testing** - Verify indexing pipeline works correctly
4. **Recovery** - Re-process files after a failed indexing run

## Proposed API Endpoints

### Force Reindex by File Paths
```http
POST /api/files/force-reindex
Content-Type: application/json

{
  "filePaths": ["/photos/image1.jpg", "/photos/image2.jpg"]
}
```
Response: `{ "markedCount": 2 }`

### Force Reindex by Directory
```http
POST /api/scan-directories/{id}/force-reindex
```
Response: `{ "markedCount": 150 }`

### Force Reindex All Files
```http
POST /api/files/force-reindex-all
```
Response: `{ "markedCount": 5000 }`

### Clear File from Index
```http
DELETE /api/files/{id}/index-state
```
Response: `204 No Content`

## Implementation Options

### Option A: Reset IndexedAt Timestamp
- Set `IndexedAt` to `DateTime.MinValue` or epoch
- Simple, no schema change
- File appears "new" to incremental indexing

### Option B: Add ForceReindexAt Column
- Add nullable `ForceReindexAt` column to `IndexedFiles` table
- Modify `CheckNeedsReindexAsync` to check this flag
- More explicit, allows tracking when force was requested

### Option C: Separate Reindex Queue Table
- Create `ReindexQueue` table with file IDs
- Indexer checks this queue in addition to modification times
- Most flexible, supports priority ordering

## UI Components

### File Detail Page
- "Force Reindex" button in file actions menu
- Shows last indexed time

### Directory Settings Page
- "Force Reindex All" button for each directory
- Confirmation dialog with file count

### Bulk Actions
- Checkbox selection in file browser
- "Force Reindex Selected" bulk action

## Acceptance Criteria

- [ ] API endpoints implemented with proper validation
- [ ] Swagger documentation for new endpoints
- [ ] UI controls in file detail and directory settings
- [ ] Confirmation dialogs for bulk operations
- [ ] Audit logging for force reindex operations
- [ ] Unit tests for service layer
- [ ] Integration tests for API endpoints

## Related

- ADR-012: Incremental Indexing with Scan Sessions
- Depends on: Incremental indexing feature (PR #143)
