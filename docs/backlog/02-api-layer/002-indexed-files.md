# 002: Indexed Files Controller

**Priority**: P0 (Critical Path)
**Agent**: A1
**Branch**: `feature/api-indexed-files`
**Estimated Complexity**: High

## Objective

Implement API endpoints for querying indexed files with pagination, filtering, and batch ingestion from the indexing service.

## Dependencies

- `01-shared-contracts/001-dtos.md`
- `02-api-layer/001-scan-directories.md` (for directory relationship)

## Acceptance Criteria

- [ ] GET /api/files - List files with pagination and filtering
- [ ] GET /api/files/{id} - Get single file details
- [ ] GET /api/files/{id}/thumbnail - Get thumbnail image
- [ ] POST /api/files/batch - Batch ingest from indexing service
- [ ] GET /api/files/stats - Get statistics (total, duplicates, size)
- [ ] Filter by: directory, hash, date range, has duplicates
- [ ] Sort by: name, date, size, indexed date
- [ ] Efficient pagination with keyset/cursor support

## TDD Steps

### Red Phase - Query Tests
```csharp
[Fact]
public async Task GetFiles_WithDirectoryFilter_ReturnsFilteredResults()
{
    // Arrange
    var service = new Mock<IIndexedFileService>();
    var query = new FileQueryParameters
    {
        DirectoryId = Guid.NewGuid(),
        Page = 1,
        PageSize = 20
    };

    // Act & Assert
}

[Fact]
public async Task GetFiles_WithDuplicatesOnlyFilter_ReturnsDuplicatesOnly()
{
    // Test filtering for files with duplicates
}
```

### Red Phase - Batch Ingest Tests
```csharp
[Fact]
public async Task BatchIngest_WithNewFiles_CreatesRecords()
{
    // Test batch insertion
}

[Fact]
public async Task BatchIngest_WithExistingHash_UpdatesRecord()
{
    // Test upsert behavior
}
```

### Green Phase
Implement service and controller.

### Refactor Phase
Optimize queries, add indexes, implement cursor pagination.

## Files to Create/Modify

```
src/Api/
├── Controllers/
│   └── IndexedFilesController.cs
├── Services/
│   ├── IIndexedFileService.cs
│   └── IndexedFileService.cs
└── Models/
    ├── FileQueryParameters.cs
    └── FileStatistics.cs

tests/Api.Tests/
├── Controllers/
│   └── IndexedFilesControllerTests.cs
└── Services/
    └── IndexedFileServiceTests.cs

tests/Integration.Tests/
└── IndexedFilesApiTests.cs
```

## API Specification

### Query Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| page | int | Page number (default: 1) |
| pageSize | int | Items per page (default: 50, max: 200) |
| directoryId | Guid? | Filter by scan directory |
| hasDuplicates | bool? | Filter files with duplicates |
| minDate | DateTime? | Filter by date taken (min) |
| maxDate | DateTime? | Filter by date taken (max) |
| search | string? | Search in file name |
| sortBy | string | name, date, size, indexed (default: indexed) |
| sortDesc | bool | Descending order (default: true) |

### Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | /api/files | List with filters |
| GET | /api/files/{id} | Get by ID |
| GET | /api/files/{id}/thumbnail | Get thumbnail (returns image) |
| POST | /api/files/batch | Batch upsert |
| GET | /api/files/stats | Get statistics |
| DELETE | /api/files/{id} | Soft delete |

### Batch Ingest Request
```json
{
  "files": [
    {
      "filePath": "/photos/family/img001.jpg",
      "sha256Hash": "abc123...",
      "fileSizeBytes": 1024000,
      "width": 4032,
      "height": 3024,
      "dateTaken": "2024-06-15T14:30:00Z",
      "fileModifiedUtc": "2024-06-15T14:30:00Z",
      "thumbnailBase64": "..."
    }
  ],
  "scanDirectoryId": "550e8400-..."
}
```

### Statistics Response
```json
{
  "totalFiles": 15420,
  "totalSizeBytes": 52428800000,
  "duplicateGroups": 342,
  "duplicateFiles": 1024,
  "potentialSavingsBytes": 2147483648,
  "lastIndexedUtc": "2024-12-20T10:30:00Z"
}
```

## Service Implementation

```csharp
public interface IIndexedFileService
{
    Task<PagedResponse<IndexedFileDto>> QueryAsync(FileQueryParameters query, CancellationToken ct);
    Task<IndexedFileDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<byte[]?> GetThumbnailAsync(Guid id, CancellationToken ct);
    Task<BatchOperationResponse> BatchIngestAsync(BatchIngestFilesRequest request, CancellationToken ct);
    Task<FileStatistics> GetStatisticsAsync(CancellationToken ct);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken ct);
}
```

## Performance Considerations

1. **Pagination**: Use keyset pagination for large datasets
2. **Thumbnails**: Stream from database, consider caching
3. **Batch Ingest**: Use `ExecuteUpdateAsync` for bulk operations
4. **Statistics**: Cache with short TTL, update on ingest

## Test Coverage

- Controller: 85% minimum
- Service: 90% minimum
- Query builder: 95% minimum (critical path)

## Completion Checklist

- [ ] Create FileQueryParameters model
- [ ] Create IIndexedFileService interface
- [ ] Implement IndexedFileService with EF Core
- [ ] Implement efficient query builder with filters
- [ ] Create IndexedFilesController
- [ ] Implement thumbnail streaming endpoint
- [ ] Implement batch ingest with upsert logic
- [ ] Implement statistics calculation
- [ ] Write comprehensive unit tests
- [ ] Write integration tests for all endpoints
- [ ] Add appropriate database indexes
- [ ] All tests passing with coverage met
- [ ] PR created and reviewed
