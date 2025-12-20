# 002: Service Integration Tests

**Priority**: P3 (Quality Assurance)
**Agent**: A5
**Branch**: `feature/integration-service-tests`
**Estimated Complexity**: Medium

## Objective

Implement integration tests for the Indexing and Cleaner services with real file system operations.

## Dependencies

- `03-indexing-service/004-indexing-worker.md`
- `04-cleaner-service/001-delete-manager.md`
- `06-integration/001-api-integration-tests.md` (WebAppFactory reuse)

## Acceptance Criteria

- [ ] Indexing Service integration with API
- [ ] File scanning with real directories
- [ ] Hash computation verification
- [ ] Metadata extraction from real images
- [ ] Cleaner Service trash operations
- [ ] Transaction logging verification
- [ ] Rollback functionality testing
- [ ] End-to-end indexing workflow

## Files to Create

```
tests/Integration.Tests/
├── Services/
│   ├── IndexingIntegrationTests.cs
│   ├── CleanerIntegrationTests.cs
│   └── EndToEndWorkflowTests.cs
└── TestResources/
    ├── images/
    │   ├── sample.jpg
    │   ├── sample.png
    │   ├── duplicate1.jpg
    │   └── duplicate2.jpg
    └── trash/
```

## Test Implementation

```csharp
// Services/IndexingIntegrationTests.cs
public class IndexingIntegrationTests : IClassFixture<WebAppFactory>, IDisposable
{
    private readonly WebAppFactory _factory;
    private readonly string _testDirectory;
    private readonly IServiceScope _scope;

    public IndexingIntegrationTests(WebAppFactory factory)
    {
        _factory = factory;
        _testDirectory = Path.Combine(Path.GetTempPath(), $"photos-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _scope = factory.Services.CreateScope();
    }

    public void Dispose()
    {
        _scope.Dispose();
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }

    [Fact]
    public async Task FileScanner_FindsAllImages()
    {
        // Arrange
        await CreateTestImagesAsync(10);
        var scanner = _scope.ServiceProvider.GetRequiredService<IFileScanner>();

        // Act
        var files = await scanner.ScanAsync(_testDirectory, CancellationToken.None).ToListAsync();

        // Assert
        files.Should().HaveCount(10);
        files.Should().OnlyContain(f => f.Extension.ToLower() is ".jpg" or ".png");
    }

    [Fact]
    public async Task HashComputer_ProducesConsistentHashes()
    {
        // Arrange
        var content = "test content"u8.ToArray();
        var file1 = await CreateTestFileAsync("test1.jpg", content);
        var file2 = await CreateTestFileAsync("test2.jpg", content);
        var computer = _scope.ServiceProvider.GetRequiredService<IHashComputer>();

        // Act
        var hash1 = await computer.ComputeAsync(file1, CancellationToken.None);
        var hash2 = await computer.ComputeAsync(file2, CancellationToken.None);

        // Assert
        hash1.Hash.Should().Be(hash2.Hash);
    }

    [Fact]
    public async Task MetadataExtractor_ExtractsFromRealImage()
    {
        // Arrange
        var imagePath = CopyTestResource("sample.jpg");
        var extractor = _scope.ServiceProvider.GetRequiredService<IMetadataExtractor>();

        // Act
        var metadata = await extractor.ExtractAsync(imagePath, CancellationToken.None);

        // Assert
        metadata.Success.Should().BeTrue();
        metadata.Width.Should().BeGreaterThan(0);
        metadata.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task IndexingOrchestrator_ProcessesDirectory()
    {
        // Arrange
        await CreateTestImagesAsync(5);
        var client = _factory.CreateClient();

        // Create scan directory via API
        var dirResponse = await client.PostAsJsonAsync("/api/scan-directories",
            new CreateScanDirectoryRequest { Path = _testDirectory, IsEnabled = true });
        var directory = await dirResponse.Content.ReadFromJsonAsync<ScanDirectoryDto>();

        var orchestrator = _scope.ServiceProvider.GetRequiredService<IIndexingOrchestrator>();

        // Act
        await orchestrator.RunIndexingCycleAsync(CancellationToken.None);

        // Assert
        var filesResponse = await client.GetAsync($"/api/files?directoryId={directory!.Id}");
        var files = await filesResponse.Content.ReadFromJsonAsync<PagedResponse<IndexedFileDto>>();
        files!.TotalItems.Should().Be(5);
    }

    private async Task CreateTestImagesAsync(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var extension = i % 2 == 0 ? ".jpg" : ".png";
            await CreateTestFileAsync($"image{i}{extension}", GenerateMinimalImage(extension));
        }
    }

    private async Task<string> CreateTestFileAsync(string name, byte[] content)
    {
        var path = Path.Combine(_testDirectory, name);
        await File.WriteAllBytesAsync(path, content);
        return path;
    }

    private string CopyTestResource(string name)
    {
        var source = Path.Combine("TestResources", "images", name);
        var dest = Path.Combine(_testDirectory, name);
        File.Copy(source, dest);
        return dest;
    }

    private static byte[] GenerateMinimalImage(string extension)
    {
        // Generate minimal valid image bytes for testing
        // ... implementation
    }
}

// Services/CleanerIntegrationTests.cs
public class CleanerIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _trashDirectory;

    public CleanerIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"cleaner-test-{Guid.NewGuid()}");
        _trashDirectory = Path.Combine(_testDirectory, ".trash");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }

    [Fact]
    public async Task DeleteManager_MovesFileToTrash()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "delete-me.jpg");
        await File.WriteAllTextAsync(filePath, "test content");
        var hash = ComputeHash(filePath);

        var options = Options.Create(new DeleteOptions { TrashDirectory = _trashDirectory });
        var hashComputer = new Mock<IHashComputer>();
        hashComputer.Setup(h => h.ComputeAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashResult { Hash = hash, Success = true });

        var logger = new TransactionLogger(_trashDirectory);
        var manager = new DeleteManager(options, hashComputer.Object, logger);

        // Act
        var result = await manager.DeleteAsync(filePath, hash, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        File.Exists(filePath).Should().BeFalse();
        Directory.EnumerateFiles(_trashDirectory, "*", SearchOption.AllDirectories)
            .Should().ContainSingle();
    }

    [Fact]
    public async Task DeleteManager_Rollback_RestoresFile()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "rollback-me.jpg");
        var content = "original content";
        await File.WriteAllTextAsync(filePath, content);
        var hash = ComputeHash(filePath);

        var manager = CreateDeleteManager(hash);

        // Act - Delete
        var deleteResult = await manager.DeleteAsync(filePath, hash, CancellationToken.None);

        // Act - Rollback
        var rollbackResult = await manager.RollbackAsync(deleteResult.TransactionId!.Value, CancellationToken.None);

        // Assert
        rollbackResult.Success.Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
        (await File.ReadAllTextAsync(filePath)).Should().Be(content);
    }

    [Fact]
    public async Task TransactionLogger_PersistsTransactions()
    {
        // Verify transactions are persisted to disk and can be read back
    }
}

// Services/EndToEndWorkflowTests.cs
public class EndToEndWorkflowTests : IClassFixture<WebAppFactory>, IDisposable
{
    [Fact]
    public async Task FullWorkflow_IndexThenCleanDuplicates()
    {
        // 1. Create test directory with duplicate images
        // 2. Add scan directory via API
        // 3. Run indexing cycle
        // 4. Verify duplicates detected
        // 5. Auto-select originals
        // 6. Delete non-originals
        // 7. Verify files in trash
        // 8. Verify database updated
    }
}
```

## Test Resources

Include real image files for testing:
- `sample.jpg` - Valid JPEG with EXIF data
- `sample.png` - Valid PNG without EXIF
- `duplicate1.jpg` / `duplicate2.jpg` - Identical content for duplicate detection
- `corrupted.jpg` - Invalid image for error handling

## Test Coverage

- Indexing integration: 85% minimum
- Cleaner integration: 90% minimum
- End-to-end workflows: 80% minimum

## Completion Checklist

- [ ] Add test resource images to project
- [ ] Create IndexingIntegrationTests
- [ ] Test file scanning with real directories
- [ ] Test hash computation consistency
- [ ] Test metadata extraction from real images
- [ ] Create CleanerIntegrationTests
- [ ] Test trash move operation
- [ ] Test rollback functionality
- [ ] Test transaction persistence
- [ ] Create EndToEndWorkflowTests
- [ ] Test full indexing workflow
- [ ] Test duplicate detection and cleanup
- [ ] Ensure test isolation and cleanup
- [ ] All tests passing
- [ ] PR created and reviewed
