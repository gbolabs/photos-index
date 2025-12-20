# 001: Shared DTOs and Contracts

**Priority**: P0 (Critical Path)
**Agent**: A1
**Branch**: `feature/api-shared-dtos`
**Estimated Complexity**: Medium

## Objective

Define all shared DTOs, request/response models, and API contracts in `src/Shared/` that all services will use. This unlocks parallel development across all tracks.

## Dependencies

- None (this is the starting point)

## Acceptance Criteria

- [ ] All DTOs compile without errors
- [ ] DTOs have proper validation attributes
- [ ] Nullable reference types enabled and properly annotated
- [ ] Unit tests verify serialization/deserialization
- [ ] XML documentation on all public types

## TDD Steps

### Red Phase
```csharp
// tests/Shared.Tests/DtoSerializationTests.cs
[Fact]
public void IndexedFileDto_ShouldSerializeAndDeserialize()
{
    var dto = new IndexedFileDto { /* properties */ };
    var json = JsonSerializer.Serialize(dto);
    var result = JsonSerializer.Deserialize<IndexedFileDto>(json);
    result.Should().BeEquivalentTo(dto);
}
```

### Green Phase
Create minimal DTOs to pass tests.

### Refactor Phase
Add validation attributes, documentation, and optimize.

## Files to Create

```
src/Shared/
├── Dtos/
│   ├── IndexedFileDto.cs
│   ├── ScanDirectoryDto.cs
│   ├── DuplicateGroupDto.cs
│   ├── FileMetadataDto.cs
│   └── ThumbnailDto.cs
├── Requests/
│   ├── CreateScanDirectoryRequest.cs
│   ├── UpdateScanDirectoryRequest.cs
│   ├── BatchIngestFilesRequest.cs
│   └── DeleteFilesRequest.cs
├── Responses/
│   ├── PagedResponse.cs
│   ├── ApiErrorResponse.cs
│   └── BatchOperationResponse.cs
└── Contracts/
    ├── IIndexingProgress.cs
    └── IFileOperationResult.cs

tests/Shared.Tests/
├── Shared.Tests.csproj
└── DtoSerializationTests.cs
```

## DTO Specifications

### IndexedFileDto
```csharp
public record IndexedFileDto
{
    public Guid Id { get; init; }
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required string Sha256Hash { get; init; }
    public long FileSizeBytes { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public DateTime? DateTaken { get; init; }
    public DateTime FileModifiedUtc { get; init; }
    public DateTime IndexedAtUtc { get; init; }
    public Guid? DuplicateGroupId { get; init; }
    public bool IsOriginal { get; init; }
}
```

### ScanDirectoryDto
```csharp
public record ScanDirectoryDto
{
    public Guid Id { get; init; }
    public required string Path { get; init; }
    public bool IsEnabled { get; init; }
    public bool IncludeSubdirectories { get; init; }
    public DateTime? LastScanUtc { get; init; }
    public int FileCount { get; init; }
    public long TotalSizeBytes { get; init; }
}
```

### PagedResponse<T>
```csharp
public record PagedResponse<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
```

## Test Coverage

- Minimum: 90% on all DTO classes
- All serialization round-trips tested
- Validation attribute behavior tested

## Completion Checklist

- [ ] Create Shared.Tests project and add to solution
- [ ] Implement all DTOs with records
- [ ] Add System.ComponentModel.DataAnnotations validations
- [ ] Add JSON serialization tests
- [ ] Add validation tests
- [ ] Update solution to include new project
- [ ] All tests passing
- [ ] PR created and reviewed
