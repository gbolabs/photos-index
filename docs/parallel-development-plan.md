# Parallel Development Plan

This document outlines how multiple agents can work in parallel using feature branches and TDD.

## Workflow

1. Each agent creates a feature branch from `main`
2. Agent follows TDD: write tests first, then implementation
3. Agent creates PR when work is complete
4. `pr.yml` workflow validates the PR (tests must pass)
5. Human reviews and merges the PR

## Branch Naming Convention

```
feature/<agent>/<short-description>
```

Examples:
- `feature/agent1/api-scan-directories`
- `feature/agent2/indexing-file-scanner`
- `feature/agent3/web-dashboard`

## Current State

**Completed (Phase 1):**
- [x] Project structure with all projects
- [x] Database entities (IndexedFile, ScanDirectory, DuplicateGroup)
- [x] Initial EF Core migration
- [x] Docker Compose setup
- [x] CI/CD workflows (pr.yml, main.yml)
- [x] Central Package Management
- [x] Basic OpenTelemetry setup in Shared

**Ready for Phase 2 Implementation:**

## Agent Assignments

### Agent 1: API - ScanDirectories CRUD
**Branch:** `feature/agent1/api-scan-directories`

**TDD Tasks:**
1. Write tests for `ScanDirectoriesController`:
   - `GET /api/scan-directories` - list all
   - `GET /api/scan-directories/{id}` - get by id
   - `POST /api/scan-directories` - create new
   - `PUT /api/scan-directories/{id}` - update
   - `DELETE /api/scan-directories/{id}` - delete
2. Implement controller and service
3. Add request/response DTOs to Shared

**Files to create:**
- `src/Api/Controllers/ScanDirectoriesController.cs`
- `src/Api/Services/IScanDirectoryService.cs`
- `src/Api/Services/ScanDirectoryService.cs`
- `src/Shared/DTOs/ScanDirectoryDto.cs`
- `src/Shared/DTOs/CreateScanDirectoryRequest.cs`
- `tests/Api.Tests/Controllers/ScanDirectoriesControllerTests.cs`

---

### Agent 2: API - IndexedFiles Endpoints
**Branch:** `feature/agent2/api-indexed-files`

**TDD Tasks:**
1. Write tests for `IndexedFilesController`:
   - `GET /api/indexed-files` - paginated list
   - `GET /api/indexed-files/{id}` - get by id
   - `GET /api/indexed-files/duplicates` - get duplicate groups
   - `POST /api/indexed-files/batch` - batch ingest (for indexing service)
2. Implement controller and service
3. Add DTOs to Shared

**Files to create:**
- `src/Api/Controllers/IndexedFilesController.cs`
- `src/Api/Services/IIndexedFileService.cs`
- `src/Api/Services/IndexedFileService.cs`
- `src/Shared/DTOs/IndexedFileDto.cs`
- `src/Shared/DTOs/DuplicateGroupDto.cs`
- `src/Shared/DTOs/PagedResult.cs`
- `tests/Api.Tests/Controllers/IndexedFilesControllerTests.cs`

---

### Agent 3: Indexing Service - File Scanner
**Branch:** `feature/agent3/indexing-file-scanner`

**TDD Tasks:**
1. Write tests for `FileScanner`:
   - Scan directory recursively
   - Filter by supported extensions
   - Skip hidden files/directories
   - Return file info (path, size, modified date)
2. Implement FileScanner service

**Supported extensions:** `.jpg`, `.jpeg`, `.png`, `.gif`, `.heic`, `.webp`, `.bmp`, `.tiff`

**Files to create:**
- `src/IndexingService/Services/IFileScanner.cs`
- `src/IndexingService/Services/FileScanner.cs`
- `src/IndexingService/Models/ScannedFile.cs`
- `tests/IndexingService.Tests/Services/FileScannerTests.cs`

---

### Agent 4: Indexing Service - Hash Computer
**Branch:** `feature/agent4/indexing-hash-computer`

**TDD Tasks:**
1. Write tests for `HashComputer`:
   - Compute SHA256 hash of file
   - Use streaming for memory efficiency
   - Handle large files (>1GB)
   - Return hash as lowercase hex string
2. Implement HashComputer service

**Files to create:**
- `src/IndexingService/Services/IHashComputer.cs`
- `src/IndexingService/Services/HashComputer.cs`
- `tests/IndexingService.Tests/Services/HashComputerTests.cs`

---

### Agent 5: Indexing Service - Metadata Extractor
**Branch:** `feature/agent5/indexing-metadata`

**TDD Tasks:**
1. Write tests for `MetadataExtractor`:
   - Extract image dimensions (width, height)
   - Extract EXIF date taken
   - Handle missing EXIF data gracefully
   - Use ImageSharp library
2. Implement MetadataExtractor service

**Files to create:**
- `src/IndexingService/Services/IMetadataExtractor.cs`
- `src/IndexingService/Services/MetadataExtractor.cs`
- `src/IndexingService/Models/ImageMetadata.cs`
- `tests/IndexingService.Tests/Services/MetadataExtractorTests.cs`

---

### Agent 6: Web - Dashboard Component
**Branch:** `feature/agent6/web-dashboard`

**TDD Tasks:**
1. Write tests for DashboardComponent:
   - Display total files count
   - Display duplicates count
   - Display storage saved
   - Show loading state
   - Handle API errors
2. Implement component and service

**Files to create:**
- `src/Web/src/app/dashboard/dashboard.component.ts`
- `src/Web/src/app/dashboard/dashboard.component.html`
- `src/Web/src/app/dashboard/dashboard.component.spec.ts`
- `src/Web/src/app/services/stats.service.ts`
- `src/Web/src/app/services/stats.service.spec.ts`

---

## TDD Guidelines

### For .NET (xUnit + FluentAssertions)

```csharp
public class ScanDirectoriesControllerTests
{
    [Fact]
    public async Task GetAll_ReturnsAllDirectories()
    {
        // Arrange
        var mockService = new Mock<IScanDirectoryService>();
        mockService.Setup(s => s.GetAllAsync())
            .ReturnsAsync(new List<ScanDirectoryDto> { ... });
        var controller = new ScanDirectoriesController(mockService.Object);

        // Act
        var result = await controller.GetAll();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }
}
```

### For Angular (Vitest)

```typescript
describe('DashboardComponent', () => {
  it('should display total files count', async () => {
    // Arrange
    const statsService = { getStats: vi.fn().mockResolvedValue({ totalFiles: 100 }) };

    // Act
    const component = new DashboardComponent(statsService);
    await component.ngOnInit();

    // Assert
    expect(component.totalFiles).toBe(100);
  });
});
```

## Coordination

- **Shared DTOs**: Agents 1 & 2 should coordinate on DTO names in `src/Shared/`
- **No EF Migrations**: Don't modify database schema without coordination
- **Run tests locally**: Before pushing, run `dotnet test` and verify tests pass

## PR Checklist

Before creating PR:
- [ ] All new code has tests
- [ ] All tests pass locally (`dotnet test` / `npm test`)
- [ ] No changes to files outside your scope
- [ ] Branch is up to date with main
- [ ] PR description explains what was implemented

## Commands for Agents

```bash
# Create feature branch
git checkout main
git pull origin main
git checkout -b feature/agent1/api-scan-directories

# Run tests during development
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ScanDirectories"

# Before pushing
dotnet test src/PhotosIndex.sln
git push -u origin feature/agent1/api-scan-directories

# Create PR (gh CLI)
gh pr create --title "Add ScanDirectories CRUD API" --body "..."
```
