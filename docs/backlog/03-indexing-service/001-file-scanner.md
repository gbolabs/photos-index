# 001: File Scanner

**Priority**: P0 (Critical Path)
**Agent**: A2
**Branch**: `feature/indexing-file-scanner`
**Estimated Complexity**: High

## Objective

Implement recursive directory scanning that discovers image files, filters by supported extensions, and handles filesystem edge cases gracefully.

## Dependencies

- `01-shared-contracts/001-dtos.md` (IIndexingProgress contract)

## Acceptance Criteria

- [ ] Recursively scan directories when configured
- [ ] Filter by supported extensions (.jpg, .jpeg, .png, .gif, .heic, .webp, .bmp, .tiff)
- [ ] Case-insensitive extension matching
- [ ] Skip hidden files and directories (configurable)
- [ ] Handle permission errors gracefully (log and continue)
- [ ] Handle symbolic links (skip or follow, configurable)
- [ ] Report progress via IIndexingProgress
- [ ] Support cancellation token
- [ ] Memory-efficient enumeration (yield return)

## TDD Steps

### Red Phase - Core Scanning
```csharp
// tests/IndexingService.Tests/FileScannerTests.cs
public class FileScannerTests
{
    [Fact]
    public async Task ScanDirectory_FindsAllImageFiles()
    {
        // Arrange
        var tempDir = CreateTempDirectoryWithImages();
        var scanner = new FileScanner(Options.Create(new ScannerOptions()));

        // Act
        var files = await scanner.ScanAsync(tempDir, CancellationToken.None).ToListAsync();

        // Assert
        files.Should().HaveCount(expectedCount);
    }

    [Fact]
    public async Task ScanDirectory_SkipsHiddenFiles()
    {
        // Create .hidden files and verify they're skipped
    }

    [Fact]
    public async Task ScanDirectory_ReportsProgress()
    {
        // Verify progress callback is invoked
    }
}
```

### Red Phase - Edge Cases
```csharp
[Fact]
public async Task ScanDirectory_HandlesPermissionDenied()
{
    // Should log error and continue with other files
}

[Fact]
public async Task ScanDirectory_SupportsCancellation()
{
    // Should stop scanning when cancelled
}

[Theory]
[InlineData(".JPG")]
[InlineData(".Jpg")]
[InlineData(".jpg")]
public async Task ScanDirectory_CaseInsensitiveExtensions(string extension)
{
    // All variations should be found
}
```

### Green Phase
Implement FileScanner service.

### Refactor Phase
Optimize enumeration, add parallel directory scanning.

## Files to Create/Modify

```
src/IndexingService/
├── Services/
│   ├── IFileScanner.cs
│   └── FileScanner.cs
├── Models/
│   ├── ScannerOptions.cs
│   ├── ScannedFile.cs
│   └── ScanProgress.cs
└── appsettings.json (add scanner options)

tests/IndexingService.Tests/
├── Services/
│   └── FileScannerTests.cs
└── TestHelpers/
    └── TempDirectoryFixture.cs
```

## Service Implementation

```csharp
public interface IFileScanner
{
    IAsyncEnumerable<ScannedFile> ScanAsync(
        string directoryPath,
        CancellationToken cancellationToken,
        IProgress<ScanProgress>? progress = null);

    IAsyncEnumerable<ScannedFile> ScanAsync(
        ScanDirectoryDto directory,
        CancellationToken cancellationToken,
        IProgress<ScanProgress>? progress = null);
}

public record ScannedFile
{
    public required string FullPath { get; init; }
    public required string FileName { get; init; }
    public required string Extension { get; init; }
    public long FileSizeBytes { get; init; }
    public DateTime LastModifiedUtc { get; init; }
}

public record ScanProgress
{
    public int FilesFound { get; init; }
    public int DirectoriesScanned { get; init; }
    public int Errors { get; init; }
    public string CurrentDirectory { get; init; } = "";
}
```

## Configuration

```json
// appsettings.json
{
  "Scanner": {
    "SupportedExtensions": [".jpg", ".jpeg", ".png", ".gif", ".heic", ".webp", ".bmp", ".tiff"],
    "SkipHiddenFiles": true,
    "SkipHiddenDirectories": true,
    "FollowSymlinks": false,
    "MaxDepth": 50
  }
}
```

## Implementation Notes

1. Use `Directory.EnumerateFiles` with `EnumerationOptions` for efficiency
2. Use `yield return` for memory-efficient streaming
3. Wrap in try-catch for individual files to handle permission errors
4. Use `Channel<T>` for parallel directory scanning if needed
5. Consider using `System.IO.Abstractions` for testability

## Test Coverage

- Core scanning: 90% minimum
- Edge cases: 100% (all error paths tested)
- Progress reporting: 80% minimum

## Completion Checklist

- [ ] Create IFileScanner interface
- [ ] Create ScannerOptions and ScannedFile models
- [ ] Implement FileScanner with recursive scanning
- [ ] Add extension filtering (case-insensitive)
- [ ] Add hidden file/directory skipping
- [ ] Add error handling for permission issues
- [ ] Add cancellation support
- [ ] Add progress reporting
- [ ] Create TempDirectoryFixture for tests
- [ ] Write unit tests for all scenarios
- [ ] Add configuration to appsettings.json
- [ ] Register in DI container
- [ ] All tests passing with coverage met
- [ ] PR created and reviewed
