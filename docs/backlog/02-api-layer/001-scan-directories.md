# 001: Scan Directories Controller

**Status**: ✅ Complete
**PR**: [#4](https://github.com/gbolabs/photos-index/pull/4)
**Priority**: P0 (Critical Path)
**Agent**: A1
**Branch**: `feature/api-scan-directories`
**Estimated Complexity**: Medium

## Objective

Implement full CRUD operations for managing scan directories. Users configure which directories to scan for photos.

## Dependencies

- `01-shared-contracts/001-dtos.md` (ScanDirectoryDto, requests, responses)

## Acceptance Criteria

- [ ] GET /api/scan-directories - List all directories (paged)
- [ ] GET /api/scan-directories/{id} - Get single directory
- [ ] POST /api/scan-directories - Create new directory
- [ ] PUT /api/scan-directories/{id} - Update directory
- [ ] DELETE /api/scan-directories/{id} - Remove directory
- [ ] POST /api/scan-directories/{id}/trigger-scan - Trigger manual scan
- [ ] Path validation (exists, readable)
- [ ] Duplicate path prevention
- [ ] Integration tests with TestContainers

## TDD Steps

### Red Phase - Unit Tests
```csharp
// tests/Api.Tests/Controllers/ScanDirectoriesControllerTests.cs
public class ScanDirectoriesControllerTests
{
    [Fact]
    public async Task GetAll_ReturnsPagedDirectories()
    {
        // Arrange
        var mockService = new Mock<IScanDirectoryService>();
        mockService.Setup(s => s.GetAllAsync(1, 10, default))
            .ReturnsAsync(new PagedResponse<ScanDirectoryDto> { ... });

        var controller = new ScanDirectoriesController(mockService.Object);

        // Act
        var result = await controller.GetAll(1, 10);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Create_WithInvalidPath_ReturnsBadRequest()
    {
        // Test path validation
    }
}
```

### Red Phase - Integration Tests
```csharp
// tests/Integration.Tests/ScanDirectoriesApiTests.cs
public class ScanDirectoriesApiTests : IClassFixture<WebAppFactory>
{
    [Fact]
    public async Task CreateAndRetrieveDirectory_Succeeds()
    {
        // Full HTTP round-trip test
    }
}
```

### Green Phase
Implement controller and service to pass tests.

### Refactor Phase
Add proper error handling, logging, and optimization.

## Files to Create/Modify

```
src/Api/
├── Controllers/
│   └── ScanDirectoriesController.cs
├── Services/
│   ├── IScanDirectoryService.cs
│   └── ScanDirectoryService.cs
└── Validators/
    └── ScanDirectoryValidator.cs

tests/Api.Tests/
├── Controllers/
│   └── ScanDirectoriesControllerTests.cs
└── Services/
    └── ScanDirectoryServiceTests.cs

tests/Integration.Tests/
├── WebAppFactory.cs
└── ScanDirectoriesApiTests.cs
```

## API Specification

### Endpoints

| Method | Route | Description | Response |
|--------|-------|-------------|----------|
| GET | /api/scan-directories | List all (paged) | `PagedResponse<ScanDirectoryDto>` |
| GET | /api/scan-directories/{id} | Get by ID | `ScanDirectoryDto` |
| POST | /api/scan-directories | Create | `ScanDirectoryDto` (201) |
| PUT | /api/scan-directories/{id} | Update | `ScanDirectoryDto` |
| DELETE | /api/scan-directories/{id} | Delete | 204 No Content |
| POST | /api/scan-directories/{id}/trigger-scan | Trigger scan | 202 Accepted |

### Request/Response Examples

```json
// POST /api/scan-directories
{
  "path": "/photos/family",
  "includeSubdirectories": true,
  "isEnabled": true
}

// Response 201
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "path": "/photos/family",
  "includeSubdirectories": true,
  "isEnabled": true,
  "lastScanUtc": null,
  "fileCount": 0,
  "totalSizeBytes": 0
}
```

## Service Implementation

```csharp
public interface IScanDirectoryService
{
    Task<PagedResponse<ScanDirectoryDto>> GetAllAsync(int page, int pageSize, CancellationToken ct);
    Task<ScanDirectoryDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<ScanDirectoryDto> CreateAsync(CreateScanDirectoryRequest request, CancellationToken ct);
    Task<ScanDirectoryDto?> UpdateAsync(Guid id, UpdateScanDirectoryRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
    Task<bool> TriggerScanAsync(Guid id, CancellationToken ct);
    Task<bool> PathExistsAsync(string path, CancellationToken ct);
}
```

## Test Coverage

- Controller: 85% minimum
- Service: 90% minimum
- Include edge cases: invalid paths, duplicates, not found

## Completion Checklist

- [ ] Create IScanDirectoryService interface
- [ ] Implement ScanDirectoryService with EF Core
- [ ] Create ScanDirectoriesController with all endpoints
- [ ] Add path validation logic
- [ ] Write unit tests for controller
- [ ] Write unit tests for service
- [ ] Create WebAppFactory for integration tests
- [ ] Write integration tests
- [ ] Register services in DI container
- [ ] All tests passing with coverage met
- [ ] PR created and reviewed
