# 002: Hash Computer

**Priority**: P1 (Core Features)
**Agent**: A2
**Branch**: `feature/indexing-hash-computer`
**Estimated Complexity**: Medium

## Objective

Implement SHA256 hash computation for files using streaming to handle large files efficiently without loading entire file into memory.

## Dependencies

- `03-indexing-service/001-file-scanner.md` (ScannedFile model)

## Acceptance Criteria

- [ ] Compute SHA256 hash for any file size
- [ ] Stream-based processing (max 8KB buffer)
- [ ] Handle files up to 10GB+
- [ ] Report progress for large files
- [ ] Support cancellation
- [ ] Handle locked files gracefully
- [ ] Memory usage under 100MB regardless of file size
- [ ] Parallel hash computation support

## TDD Steps

### Red Phase - Core Hashing
```csharp
// tests/IndexingService.Tests/HashComputerTests.cs
public class HashComputerTests
{
    [Fact]
    public async Task ComputeHash_ReturnsCorrectSha256()
    {
        // Arrange
        var content = "test content"u8.ToArray();
        var expectedHash = SHA256.HashData(content);
        var tempFile = CreateTempFile(content);
        var computer = new HashComputer();

        // Act
        var result = await computer.ComputeAsync(tempFile, CancellationToken.None);

        // Assert
        result.Hash.Should().Be(Convert.ToHexString(expectedHash).ToLowerInvariant());
    }

    [Fact]
    public async Task ComputeHash_LargeFile_StreamsEfficiently()
    {
        // Create 100MB file, verify memory stays under limit
    }
}
```

### Red Phase - Edge Cases
```csharp
[Fact]
public async Task ComputeHash_FileNotFound_ThrowsFileNotFoundException()
{
}

[Fact]
public async Task ComputeHash_FileLocked_RetriesOrFails()
{
}

[Fact]
public async Task ComputeHash_Cancellation_StopsProcessing()
{
}
```

### Green Phase
Implement HashComputer service.

### Refactor Phase
Add parallel batch processing, optimize buffer size.

## Files to Create/Modify

```
src/IndexingService/
├── Services/
│   ├── IHashComputer.cs
│   └── HashComputer.cs
└── Models/
    └── HashResult.cs

tests/IndexingService.Tests/
├── Services/
│   └── HashComputerTests.cs
└── TestHelpers/
    └── LargeFileGenerator.cs
```

## Service Implementation

```csharp
public interface IHashComputer
{
    Task<HashResult> ComputeAsync(
        string filePath,
        CancellationToken cancellationToken,
        IProgress<HashProgress>? progress = null);

    IAsyncEnumerable<HashResult> ComputeBatchAsync(
        IEnumerable<string> filePaths,
        int maxParallelism,
        CancellationToken cancellationToken);
}

public record HashResult
{
    public required string FilePath { get; init; }
    public required string Hash { get; init; }  // Lowercase hex string
    public long BytesProcessed { get; init; }
    public TimeSpan Duration { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
}

public record HashProgress
{
    public long BytesProcessed { get; init; }
    public long TotalBytes { get; init; }
    public double PercentComplete => TotalBytes > 0 ? (double)BytesProcessed / TotalBytes * 100 : 0;
}
```

## Implementation Details

```csharp
public class HashComputer : IHashComputer
{
    private const int BufferSize = 81920; // 80KB optimal for disk I/O

    public async Task<HashResult> ComputeAsync(
        string filePath,
        CancellationToken cancellationToken,
        IProgress<HashProgress>? progress = null)
    {
        var stopwatch = Stopwatch.StartNew();

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.SequentialScan | FileOptions.Asynchronous);

        using var sha256 = SHA256.Create();
        var buffer = new byte[BufferSize];
        long totalBytes = stream.Length;
        long bytesRead = 0;
        int read;

        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            sha256.TransformBlock(buffer, 0, read, null, 0);
            bytesRead += read;
            progress?.Report(new HashProgress { BytesProcessed = bytesRead, TotalBytes = totalBytes });
        }

        sha256.TransformFinalBlock([], 0, 0);

        return new HashResult
        {
            FilePath = filePath,
            Hash = Convert.ToHexString(sha256.Hash!).ToLowerInvariant(),
            BytesProcessed = bytesRead,
            Duration = stopwatch.Elapsed,
            Success = true
        };
    }
}
```

## Parallel Processing

```csharp
public IAsyncEnumerable<HashResult> ComputeBatchAsync(
    IEnumerable<string> filePaths,
    int maxParallelism,
    CancellationToken cancellationToken)
{
    return filePaths
        .ToAsyncEnumerable()
        .SelectAwaitWithCancellation(async (path, ct) =>
            await ComputeAsync(path, ct))
        .WithConcurrencyLimit(maxParallelism);
}
```

## Performance Benchmarks

Add benchmarks for:
- Small files (< 1MB)
- Medium files (1-100MB)
- Large files (> 100MB)
- Parallel vs sequential processing

## Test Coverage

- Core hashing: 95% minimum
- Edge cases: 100%
- Parallel processing: 80% minimum

## Completion Checklist

- [ ] Create IHashComputer interface
- [ ] Create HashResult and HashProgress models
- [ ] Implement streaming SHA256 computation
- [ ] Add progress reporting
- [ ] Add cancellation support
- [ ] Implement batch parallel processing
- [ ] Handle file lock scenarios
- [ ] Write unit tests
- [ ] Write memory usage tests
- [ ] Add benchmarks
- [ ] Register in DI container
- [ ] All tests passing with coverage met
- [ ] PR created and reviewed
