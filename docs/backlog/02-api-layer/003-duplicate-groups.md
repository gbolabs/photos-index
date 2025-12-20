# 003: Duplicate Groups Controller

**Priority**: P1 (Core Features)
**Agent**: A1
**Branch**: `feature/api-duplicate-groups`
**Estimated Complexity**: Medium

## Objective

Implement API endpoints for managing duplicate groups, selecting originals, and preparing files for deletion.

## Dependencies

- `01-shared-contracts/001-dtos.md`
- `02-api-layer/002-indexed-files.md`

## Acceptance Criteria

- [ ] GET /api/duplicates - List duplicate groups (paged)
- [ ] GET /api/duplicates/{id} - Get group with all members
- [ ] PUT /api/duplicates/{id}/original - Set original file in group
- [ ] POST /api/duplicates/{id}/auto-select - Auto-select original (rules-based)
- [ ] GET /api/duplicates/stats - Duplicate statistics
- [ ] DELETE /api/duplicates/{id}/non-originals - Queue non-originals for deletion
- [ ] Duplicate detection runs automatically on hash collision

## TDD Steps

### Red Phase
```csharp
[Fact]
public async Task GetDuplicateGroups_ReturnsPaginatedGroups()
{
    // Test listing duplicate groups
}

[Fact]
public async Task SetOriginal_UpdatesGroupCorrectly()
{
    // Test setting a file as original
}

[Fact]
public async Task AutoSelect_ChoosesEarliestDate()
{
    // Test auto-selection algorithm
}
```

### Green Phase
Implement service and controller.

### Refactor Phase
Optimize duplicate detection, add caching.

## Files to Create/Modify

```
src/Api/
├── Controllers/
│   └── DuplicateGroupsController.cs
├── Services/
│   ├── IDuplicateService.cs
│   └── DuplicateService.cs
└── Models/
    └── DuplicateAutoSelectRules.cs

tests/Api.Tests/
├── Controllers/
│   └── DuplicateGroupsControllerTests.cs
└── Services/
    └── DuplicateServiceTests.cs
```

## API Specification

### Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | /api/duplicates | List groups (paged) |
| GET | /api/duplicates/{id} | Get group with files |
| PUT | /api/duplicates/{id}/original | Set original file |
| POST | /api/duplicates/{id}/auto-select | Auto-select original |
| POST | /api/duplicates/auto-select-all | Auto-select for all groups |
| GET | /api/duplicates/stats | Statistics |
| DELETE | /api/duplicates/{id}/non-originals | Queue for deletion |

### Response Models

```json
// GET /api/duplicates/{id}
{
  "id": "550e8400-...",
  "sha256Hash": "abc123...",
  "fileCount": 3,
  "totalSizeBytes": 3072000,
  "potentialSavingsBytes": 2048000,
  "originalFileId": "660e8400-...",
  "files": [
    {
      "id": "660e8400-...",
      "filePath": "/photos/2024/img001.jpg",
      "isOriginal": true,
      "dateTaken": "2024-06-15T14:30:00Z",
      "fileSizeBytes": 1024000
    },
    {
      "id": "770e8400-...",
      "filePath": "/photos/backup/img001.jpg",
      "isOriginal": false,
      "dateTaken": "2024-06-15T14:30:00Z",
      "fileSizeBytes": 1024000
    }
  ]
}
```

### Auto-Select Rules
```json
// POST /api/duplicates/{id}/auto-select
{
  "preferredDirectoryPatterns": ["/photos/originals/*"],
  "strategy": "earliest_date" // or "shortest_path", "preferred_directory"
}
```

## Service Implementation

```csharp
public interface IDuplicateService
{
    Task<PagedResponse<DuplicateGroupDto>> GetGroupsAsync(int page, int pageSize, CancellationToken ct);
    Task<DuplicateGroupDto?> GetGroupAsync(Guid id, CancellationToken ct);
    Task<bool> SetOriginalAsync(Guid groupId, Guid fileId, CancellationToken ct);
    Task<Guid?> AutoSelectOriginalAsync(Guid groupId, DuplicateAutoSelectRules rules, CancellationToken ct);
    Task<int> AutoSelectAllAsync(DuplicateAutoSelectRules rules, CancellationToken ct);
    Task<DuplicateStatistics> GetStatisticsAsync(CancellationToken ct);
    Task<int> QueueNonOriginalsForDeletionAsync(Guid groupId, CancellationToken ct);
}
```

## Auto-Selection Algorithm

1. **Preferred Directory**: If file is in preferred directory pattern, select it
2. **Earliest Date Taken**: Select file with earliest EXIF date
3. **Shortest Path**: Select file with shortest path (fewer nested directories)
4. **First Indexed**: Fallback to first file indexed

## Test Coverage

- Controller: 85% minimum
- Service: 90% minimum
- Auto-select algorithm: 100% (business-critical)

## Completion Checklist

- [ ] Create IDuplicateService interface
- [ ] Implement DuplicateService
- [ ] Create DuplicateGroupsController
- [ ] Implement auto-selection algorithm
- [ ] Write unit tests for controller
- [ ] Write unit tests for service
- [ ] Write integration tests
- [ ] Document API in OpenAPI/Swagger
- [ ] All tests passing with coverage met
- [ ] PR created and reviewed
