# 001: Delete Manager

**Status**: ✅ Complete
**Priority**: P1 (Core Features)
**Agent**: A3
**Branch**: `feature/v0.3.0-distributed-processing`
**Estimated Complexity**: High

## Objective

Implement safe file deletion with API-driven archive workflow, category-based retention, and dry-run capability.

> **Note**: The implementation evolved from the original design. Instead of local `.trash` directories:
> - Files are uploaded to TrueNAS MinIO archive bucket before deletion
> - API controls retention policies (6 months for hash duplicates, 2 years for near/manual)
> - CleanerService runs on Synology with RW access, communicates via SignalR
> - See [ADR-013](../adrs/013-cleaner-service-architecture.md) for full architecture.

## Dependencies

- `01-shared-contracts/001-dtos.md`
- `02-api-layer/002-indexed-files.md`

## Acceptance Criteria

- [ ] Soft delete: Move files to trash directory instead of permanent delete
- [ ] Transaction logging: Record all operations for audit/rollback
- [ ] Dry-run mode: Simulate deletion without actually moving files
- [ ] Rollback capability: Restore files from trash
- [ ] Configurable trash retention period
- [ ] Poll API for files queued for deletion
- [ ] Update API after successful deletion
- [ ] Handle permission errors gracefully
- [ ] Verify file hash before deletion (safety check)

## TDD Steps

### Red Phase - Core Deletion
```csharp
// tests/CleanerService.Tests/DeleteManagerTests.cs
public class DeleteManagerTests
{
    [Fact]
    public async Task DeleteFile_MovesToTrash()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var trashDir = Path.Combine(tempDir, ".trash");
        var file = CreateTestFile(tempDir, "test.jpg");
        var manager = new DeleteManager(Options.Create(new DeleteOptions { TrashDirectory = trashDir }));

        // Act
        var result = await manager.DeleteAsync(file, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        File.Exists(file).Should().BeFalse();
        Directory.GetFiles(trashDir).Should().ContainSingle();
    }

    [Fact]
    public async Task DeleteFile_DryRun_DoesNotDelete()
    {
        // File should still exist after dry run
    }
}
```

### Red Phase - Transaction Logging
```csharp
[Fact]
public async Task DeleteFile_CreatesTransactionLog()
{
    // Verify transaction log entry created
}

[Fact]
public async Task Rollback_RestoresFile()
{
    // Delete then rollback, verify file restored
}
```

### Red Phase - Safety Checks
```csharp
[Fact]
public async Task DeleteFile_HashMismatch_Aborts()
{
    // If file changed since indexed, abort deletion
}

[Fact]
public async Task DeleteFile_PermissionDenied_ReturnsError()
{
    // Handle permission errors gracefully
}
```

### Green Phase
Implement DeleteManager service.

### Refactor Phase
Add batch operations, optimize logging.

## Files to Create/Modify

```
src/CleanerService/
├── Worker.cs (modify)
├── Services/
│   ├── IDeleteManager.cs
│   ├── DeleteManager.cs
│   ├── ITransactionLogger.cs
│   └── TransactionLogger.cs
├── ApiClient/
│   ├── ICleanerApiClient.cs
│   └── CleanerApiClient.cs
└── Models/
    ├── DeleteOptions.cs
    ├── DeleteResult.cs
    ├── DeleteTransaction.cs
    └── RollbackRequest.cs

tests/CleanerService.Tests/
├── Services/
│   ├── DeleteManagerTests.cs
│   └── TransactionLoggerTests.cs
└── TestHelpers/
    └── TempFileFixture.cs
```

## Service Implementation

```csharp
public interface IDeleteManager
{
    Task<DeleteResult> DeleteAsync(string filePath, string expectedHash, CancellationToken ct);
    Task<IReadOnlyList<DeleteResult>> DeleteBatchAsync(IEnumerable<DeleteRequest> requests, CancellationToken ct);
    Task<DeleteResult> DeleteDryRunAsync(string filePath, CancellationToken ct);
    Task<RollbackResult> RollbackAsync(Guid transactionId, CancellationToken ct);
    Task<int> CleanupTrashAsync(TimeSpan olderThan, CancellationToken ct);
}

public record DeleteResult
{
    public required string FilePath { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
    public Guid? TransactionId { get; init; }
    public string? TrashPath { get; init; }
    public long FileSizeBytes { get; init; }
}

public record DeleteTransaction
{
    public Guid Id { get; init; }
    public required string OriginalPath { get; init; }
    public required string TrashPath { get; init; }
    public required string FileHash { get; init; }
    public DateTime DeletedAtUtc { get; init; }
    public bool IsRolledBack { get; init; }
    public DateTime? RolledBackAtUtc { get; init; }
}
```

## Delete Manager Implementation

```csharp
public class DeleteManager : IDeleteManager
{
    private readonly ITransactionLogger _transactionLogger;
    private readonly IHashComputer _hashComputer;
    private readonly DeleteOptions _options;

    public async Task<DeleteResult> DeleteAsync(string filePath, string expectedHash, CancellationToken ct)
    {
        // Verify file exists
        if (!File.Exists(filePath))
            return new DeleteResult { FilePath = filePath, Success = false, Error = "File not found" };

        // Safety check: verify hash matches
        if (_options.VerifyHashBeforeDelete)
        {
            var currentHash = await _hashComputer.ComputeAsync(filePath, ct);
            if (currentHash.Hash != expectedHash)
            {
                return new DeleteResult
                {
                    FilePath = filePath,
                    Success = false,
                    Error = "Hash mismatch - file has been modified"
                };
            }
        }

        // Create trash directory structure
        var trashPath = GenerateTrashPath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(trashPath)!);

        // Move to trash
        File.Move(filePath, trashPath);

        // Log transaction
        var transaction = await _transactionLogger.LogDeleteAsync(
            filePath, trashPath, expectedHash, ct);

        return new DeleteResult
        {
            FilePath = filePath,
            Success = true,
            TransactionId = transaction.Id,
            TrashPath = trashPath
        };
    }

    private string GenerateTrashPath(string originalPath)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var fileName = Path.GetFileName(originalPath);
        var relativePath = originalPath.Replace("/", "_").Replace("\\", "_");
        return Path.Combine(_options.TrashDirectory, timestamp, relativePath, fileName);
    }
}
```

## Transaction Logger

```csharp
public interface ITransactionLogger
{
    Task<DeleteTransaction> LogDeleteAsync(string originalPath, string trashPath, string hash, CancellationToken ct);
    Task<DeleteTransaction?> GetTransactionAsync(Guid id, CancellationToken ct);
    Task MarkRolledBackAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<DeleteTransaction>> GetTransactionsAsync(DateTime since, CancellationToken ct);
}
```

## Configuration

```json
{
  "Cleaner": {
    "TrashDirectory": "/photos/.trash",
    "RetentionDays": 30,
    "VerifyHashBeforeDelete": true,
    "PollIntervalMinutes": 5,
    "BatchSize": 50
  }
}
```

## Test Coverage

- DeleteManager: 90% minimum
- TransactionLogger: 90% minimum
- Rollback: 100%
- Safety checks: 100%

## Completion Checklist

- [x] Create CleanerJob and CleanerJobFile database entities
- [x] Create CleanerStatusDto, CleanerJobDto, DeleteFileRequest DTOs
- [x] Create CleanerHub SignalR hub for API-Cleaner communication
- [x] Create CleanerController with archive upload endpoint
- [x] Implement CleanerBackgroundService for bulk job processing
- [x] Implement RetentionBackgroundService for daily cleanup
- [x] Implement CleanerService SignalR client
- [x] Implement DeleteService with hash verification
- [x] Add dry-run mode (default: enabled)
- [x] Add category-based archive paths (hash_duplicate, near_duplicate, manual)
- [x] Implement file upload to MinIO archive bucket
- [x] Add OpenTelemetry tracing
- [x] Update Synology deployment with CleanerService (RW mounts)
- [x] Update TrueNAS deployment with cleaner configuration
- [x] Create ADR-013 documenting architecture
- [ ] Write comprehensive unit tests
- [ ] All tests passing with coverage met
- [ ] PR created and reviewed
