# 005: Cleanup History & Audit View

**Status**: 🔲 Not Started
**Priority**: P3
**Issue**: [#68](https://github.com/gbolabs/photos-index/issues/68)
**Branch**: `feature/cleanup-history`
**Estimated Complexity**: Medium
**Target Release**: v0.4.0

## Objective

Provide an audit trail of cleaned duplicates, showing what was kept vs removed, with export capabilities for record-keeping.

## Dependencies

- `12-002` Batch Validation (status field)
- `04-001` Cleaner Service (performs actual cleanup)

## Acceptance Criteria

- [ ] CleanupHistory database table
- [ ] History tab/page in duplicates section
- [ ] List view: Date, Action, Kept file, Removed files
- [ ] Filter by date range
- [ ] Search by file path
- [ ] Summary stats: space recovered, files removed
- [ ] Export to CSV and JSON
- [ ] Pagination for large history
- [ ] Color coding: kept (green), removed (grey)

## Technical Design

### Database Schema

```sql
CREATE TABLE "CleanupHistory" (
    "Id" UUID PRIMARY KEY,
    "DuplicateGroupId" UUID NOT NULL,
    "DuplicateGroupHash" VARCHAR(64) NOT NULL,  -- For reference after group deletion
    "KeptFilePath" VARCHAR(1000) NOT NULL,
    "KeptFileId" UUID NULL,  -- May be null if file later deleted
    "RemovedFilePaths" TEXT[] NOT NULL,
    "RemovedFileIds" UUID[] NOT NULL,
    "SpaceRecovered" BIGINT NOT NULL,
    "CleanedAt" TIMESTAMP NOT NULL,
    "CleanedBy" VARCHAR(100) NULL,  -- Future: user tracking
    FOREIGN KEY ("DuplicateGroupId") REFERENCES "DuplicateGroups"("Id") ON DELETE SET NULL
);

CREATE INDEX idx_cleanup_history_cleaned_at ON "CleanupHistory"("CleanedAt" DESC);
CREATE INDEX idx_cleanup_history_kept_path ON "CleanupHistory"("KeptFilePath");
```

### History View UI

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ 📜 Cleanup History                                    Export: [CSV] [JSON]  │
├─────────────────────────────────────────────────────────────────────────────┤
│ Filter: [Last 7 days ▾] Search: [________________] [🔍]                    │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│ Summary: 12,847 files removed │ 45.2 GB recovered │ Since Dec 1, 2024     │
│                                                                             │
├───────────┬───────────┬─────────────────────────┬───────────────────────────┤
│ Date      │ Action    │ Kept (Original)         │ Removed                   │
├───────────┼───────────┼─────────────────────────┼───────────────────────────┤
│ Dec 22    │ Cleaned   │ 🟢 /photos/IMG_001.jpg  │ 🗑️ /backup/IMG_001.jpg    │
│ 14:32:15  │           │    4.2 MB               │ 🗑️ /cloud/IMG_001.jpg     │
├───────────┼───────────┼─────────────────────────┼───────────────────────────┤
│ Dec 22    │ Cleaned   │ 🟢 /photos/vacation.png │ 🗑️ /public/vacation.png   │
│ 14:32:14  │           │    8.1 MB               │                           │
├───────────┼───────────┼─────────────────────────┼───────────────────────────┤
│ Dec 22    │ Validated │ 🟣 /photos/beach.jpg    │ ⏳ /duplicates/beach.jpg  │
│ 14:30:00  │           │    (pending cleanup)    │    (queued)               │
└───────────┴───────────┴─────────────────────────┴───────────────────────────┘
                                                      [< Prev] Page 1 of 257 [Next >]
```

### API Endpoints

```
GET /api/cleanup-history?page=1&pageSize=50&from=2024-12-01&to=2024-12-22
Response: {
  items: CleanupHistoryDto[],
  totalCount: number,
  summary: {
    filesRemoved: number,
    spaceRecovered: number,
    oldestEntry: Date,
    newestEntry: Date
  }
}

GET /api/cleanup-history/export?format=csv|json&from=...&to=...
Response: File download

GET /api/cleanup-history/summary
Response: {
  totalFilesRemoved: number,
  totalSpaceRecovered: number,
  byMonth: { month: string, count: number, space: number }[]
}
```

### Export Format (CSV)

```csv
CleanedAt,KeptFilePath,KeptSize,RemovedFilePaths,SpaceRecovered
2024-12-22T14:32:15Z,/photos/IMG_001.jpg,4200000,"/backup/IMG_001.jpg,/cloud/IMG_001.jpg",8400000
2024-12-22T14:32:14Z,/photos/vacation.png,8100000,/public/vacation.png,8100000
```

## Files to Create/Modify

### Backend
- `src/Database/Entities/CleanupHistory.cs` (new)
- `src/Database/Migrations/YYYYMMDD_AddCleanupHistory.cs` (new)
- `src/Api/Controllers/CleanupHistoryController.cs` (new)
- `src/Api/Services/CleanupHistoryService.cs` (new)
- `src/Shared/Dtos/CleanupHistoryDto.cs` (new)

### Frontend
- `src/Web/src/app/features/duplicates/pages/cleanup-history/` (new)
- `src/Web/src/app/services/cleanup-history.service.ts` (new)
- Update routes to include history page

## Integration with Cleaner Service

The Cleaner Service will call an internal API or directly insert into CleanupHistory when files are deleted:

```csharp
// In CleanerService after successful deletion
await _cleanupHistoryService.RecordCleanup(new CleanupRecord
{
    DuplicateGroupId = group.Id,
    KeptFile = keptFile,
    RemovedFiles = removedFiles,
    SpaceRecovered = removedFiles.Sum(f => f.FileSize)
});
```

## Test Coverage

- History recording tests
- Query and filter tests
- Export format tests
- Summary calculation tests
- Pagination tests
