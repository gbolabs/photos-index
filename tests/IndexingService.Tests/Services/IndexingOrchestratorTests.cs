using FluentAssertions;
using IndexingService.ApiClient;
using IndexingService.Models;
using IndexingService.Services;
using IndexingService.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Shared.Dtos;
using Shared.Requests;
using Shared.Responses;
using Xunit;

namespace IndexingService.Tests.Services;

public class IndexingOrchestratorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture;
    private readonly Mock<IPhotosApiClient> _mockApiClient;
    private readonly Mock<IFileScanner> _mockFileScanner;
    private readonly Mock<IHashComputer> _mockHashComputer;
    private readonly Mock<IMetadataExtractor> _mockMetadataExtractor;
    private readonly ScanSessionService _scanSession;
    private readonly Mock<ILogger<IndexingOrchestrator>> _mockLogger;
    private readonly IndexingOptions _options;

    public IndexingOrchestratorTests()
    {
        _fixture = new TempDirectoryFixture();
        _mockApiClient = new Mock<IPhotosApiClient>();
        _mockFileScanner = new Mock<IFileScanner>();
        _mockHashComputer = new Mock<IHashComputer>();
        _mockMetadataExtractor = new Mock<IMetadataExtractor>();
        _scanSession = new ScanSessionService(); // Use real session service
        _mockLogger = new Mock<ILogger<IndexingOrchestrator>>();
        _options = new IndexingOptions
        {
            BatchSize = 10,
            MaxParallelism = 2,
            ExtractMetadata = true, // Use local mode to avoid file stream issues
            GenerateThumbnails = false
        };
    }

    public void Dispose() => _fixture.Dispose();

    private IndexingOrchestrator CreateOrchestrator(IndexingOptions? options = null)
    {
        return new IndexingOrchestrator(
            _mockApiClient.Object,
            _mockFileScanner.Object,
            _mockHashComputer.Object,
            _mockMetadataExtractor.Object,
            _scanSession,
            _mockLogger.Object,
            Options.Create(options ?? _options));
    }

    #region RunIndexingCycleAsync Tests

    [Fact]
    public async Task RunIndexingCycleAsync_StartsNewSession()
    {
        // Arrange
        var initialSessionId = _scanSession.SessionId;
        _mockApiClient.Setup(c => c.GetEnabledScanDirectoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScanDirectoryDto>());

        var orchestrator = CreateOrchestrator();

        // Act
        await orchestrator.RunIndexingCycleAsync(CancellationToken.None);

        // Assert
        _scanSession.SessionId.Should().NotBe(initialSessionId);
    }

    [Fact]
    public async Task RunIndexingCycleAsync_ReturnsEmpty_WhenNoDirectoriesEnabled()
    {
        // Arrange
        _mockApiClient.Setup(c => c.GetEnabledScanDirectoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScanDirectoryDto>());

        var orchestrator = CreateOrchestrator();

        // Act
        var result = await orchestrator.RunIndexingCycleAsync(CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RunIndexingCycleAsync_ClearsSessionData_BetweenCycles()
    {
        // Arrange
        _mockApiClient.Setup(c => c.GetEnabledScanDirectoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScanDirectoryDto>());

        var orchestrator = CreateOrchestrator();

        // Act - first cycle
        _scanSession.MarkDirectoryScanned("/some/path");
        _scanSession.MarkFileProcessed("/some/file.jpg");
        await orchestrator.RunIndexingCycleAsync(CancellationToken.None);

        // Assert - session should be reset
        _scanSession.IsDirectoryScanned("/some/path").Should().BeFalse();
        _scanSession.IsFileProcessed("/some/file.jpg").Should().BeFalse();
    }

    #endregion

    #region Directory Skip Tests

    [Fact]
    public async Task IndexDirectoryAsync_SkipsDirectory_WhenCoveredByScannedParent()
    {
        // Arrange
        var parentDir = _fixture.CreateSubdirectory("photos");
        var childDir = _fixture.CreateSubdirectory("photos/2023");
        var directoryId = Guid.NewGuid();

        // Mark parent as scanned
        _scanSession.MarkDirectoryScanned(parentDir);

        var orchestrator = CreateOrchestrator();

        // Act
        var result = await orchestrator.IndexDirectoryAsync(directoryId, childDir, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.FilesScanned.Should().Be(0);
        result.FilesProcessed.Should().Be(0);

        // Should not scan files
        _mockFileScanner.Verify(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<ScanProgress>?>()), Times.Never);
    }

    [Fact]
    public async Task IndexDirectoryAsync_MarksDirectoryScanned_WhenComplete()
    {
        // Arrange
        var directoryPath = _fixture.CreateSubdirectory("photos");
        var directoryId = Guid.NewGuid();

        _mockFileScanner.Setup(s => s.ScanAsync(directoryPath, true, It.IsAny<CancellationToken>(), null))
            .Returns(AsyncEnumerable.Empty<ScannedFile>());
        _mockApiClient.Setup(c => c.UpdateLastScannedAsync(directoryId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var orchestrator = CreateOrchestrator();

        // Act
        await orchestrator.IndexDirectoryAsync(directoryId, directoryPath, CancellationToken.None);

        // Assert
        _scanSession.IsDirectoryScanned(directoryPath).Should().BeTrue();
    }

    [Fact]
    public async Task IndexDirectoryAsync_ReturnsFailure_WhenDirectoryDoesNotExist()
    {
        // Arrange
        var directoryId = Guid.NewGuid();
        var nonExistentPath = Path.Combine(_fixture.RootPath, "does-not-exist");

        var orchestrator = CreateOrchestrator();

        // Act
        var result = await orchestrator.IndexDirectoryAsync(directoryId, nonExistentPath, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("does not exist");
    }

    #endregion

    #region File Skip Tests (Session-level)

    [Fact]
    public async Task IndexDirectoryAsync_SkipsFiles_WhenAlreadyProcessedInSession()
    {
        // Arrange
        var directoryPath = _fixture.CreateSubdirectory("photos");
        var file1 = _fixture.CreateFile("photos/image1.jpg");
        var file2 = _fixture.CreateFile("photos/image2.jpg");
        var file3 = _fixture.CreateFile("photos/image3.jpg");
        var directoryId = Guid.NewGuid();

        var files = CreateScannedFiles(directoryPath, new[] { file1, file2, file3 });

        // Mark first file as already processed in session
        _scanSession.MarkFileProcessed(file1);

        _mockFileScanner.Setup(s => s.ScanAsync(directoryPath, true, It.IsAny<CancellationToken>(), null))
            .Returns(files.ToAsyncEnumerable());

        // Remaining files need reindex
        _mockApiClient.Setup(c => c.CheckFilesNeedReindexAsync(It.IsAny<CheckFilesNeedReindexRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckFilesNeedReindexRequest req, CancellationToken _) =>
                req.Files.Select(f => new FileNeedsReindexDto
                {
                    FilePath = f.FilePath,
                    LastModifiedAt = f.ModifiedAt,
                    NeedsReindex = true
                }).ToList());

        SetupHashComputerForFiles(new[] { file2, file3 });
        SetupMetadataExtractor();
        SetupBatchIngest();

        _mockApiClient.Setup(c => c.UpdateLastScannedAsync(directoryId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var orchestrator = CreateOrchestrator();

        // Act
        await orchestrator.IndexDirectoryAsync(directoryId, directoryPath, CancellationToken.None);

        // Assert - API should only receive 2 files for reindex check (not the session-skipped one)
        _mockApiClient.Verify(c => c.CheckFilesNeedReindexAsync(
            It.Is<CheckFilesNeedReindexRequest>(r => r.Files.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Incremental Indexing Tests

    [Fact]
    public async Task IndexDirectoryAsync_SkipsUnchangedFiles()
    {
        // Arrange
        var directoryPath = _fixture.CreateSubdirectory("photos");
        var filePaths = new[]
        {
            _fixture.CreateFile("photos/image1.jpg"),
            _fixture.CreateFile("photos/image2.jpg"),
            _fixture.CreateFile("photos/image3.jpg"),
            _fixture.CreateFile("photos/image4.jpg"),
            _fixture.CreateFile("photos/image5.jpg")
        };
        var directoryId = Guid.NewGuid();

        var files = CreateScannedFiles(directoryPath, filePaths);

        _mockFileScanner.Setup(s => s.ScanAsync(directoryPath, true, It.IsAny<CancellationToken>(), null))
            .Returns(files.ToAsyncEnumerable());

        // Only 2 of 5 files need reindex
        _mockApiClient.Setup(c => c.CheckFilesNeedReindexAsync(It.IsAny<CheckFilesNeedReindexRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckFilesNeedReindexRequest req, CancellationToken _) =>
                req.Files.Select((f, idx) => new FileNeedsReindexDto
                {
                    FilePath = f.FilePath,
                    LastModifiedAt = f.ModifiedAt,
                    NeedsReindex = idx < 2 // First 2 need reindex
                }).ToList());

        SetupHashComputerForFiles(filePaths.Take(2).ToArray());
        SetupMetadataExtractor();
        SetupBatchIngest();

        _mockApiClient.Setup(c => c.UpdateLastScannedAsync(directoryId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var orchestrator = CreateOrchestrator();

        // Act
        var result = await orchestrator.IndexDirectoryAsync(directoryId, directoryPath, CancellationToken.None);

        // Assert
        result.FilesScanned.Should().Be(5);
        // Only 2 files should be hashed (the ones needing reindex)
        _mockHashComputer.Verify(h => h.ComputeBatchAsync(
            It.Is<IEnumerable<string>>(paths => paths.Count() == 2),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IndexDirectoryAsync_MarksFilesProcessed_AfterSuccessfulIngest()
    {
        // Arrange
        var directoryPath = _fixture.CreateSubdirectory("photos");
        var file1 = _fixture.CreateFile("photos/image1.jpg");
        var file2 = _fixture.CreateFile("photos/image2.jpg");
        var directoryId = Guid.NewGuid();

        var files = CreateScannedFiles(directoryPath, new[] { file1, file2 });

        _mockFileScanner.Setup(s => s.ScanAsync(directoryPath, true, It.IsAny<CancellationToken>(), null))
            .Returns(files.ToAsyncEnumerable());

        _mockApiClient.Setup(c => c.CheckFilesNeedReindexAsync(It.IsAny<CheckFilesNeedReindexRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckFilesNeedReindexRequest req, CancellationToken _) =>
                req.Files.Select(f => new FileNeedsReindexDto
                {
                    FilePath = f.FilePath,
                    LastModifiedAt = f.ModifiedAt,
                    NeedsReindex = true
                }).ToList());

        SetupHashComputerForFiles(new[] { file1, file2 });
        SetupMetadataExtractor();
        SetupBatchIngest();

        _mockApiClient.Setup(c => c.UpdateLastScannedAsync(directoryId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var orchestrator = CreateOrchestrator();

        // Act
        await orchestrator.IndexDirectoryAsync(directoryId, directoryPath, CancellationToken.None);

        // Assert - both files should be marked as processed
        _scanSession.IsFileProcessed(file1).Should().BeTrue();
        _scanSession.IsFileProcessed(file2).Should().BeTrue();
    }

    #endregion

    #region Fallback Behavior Tests

    [Fact]
    public async Task IndexDirectoryAsync_ProcessesAllFiles_WhenReindexCheckFails()
    {
        // Arrange
        var directoryPath = _fixture.CreateSubdirectory("photos");
        var filePaths = new[]
        {
            _fixture.CreateFile("photos/image1.jpg"),
            _fixture.CreateFile("photos/image2.jpg"),
            _fixture.CreateFile("photos/image3.jpg")
        };
        var directoryId = Guid.NewGuid();

        var files = CreateScannedFiles(directoryPath, filePaths);

        _mockFileScanner.Setup(s => s.ScanAsync(directoryPath, true, It.IsAny<CancellationToken>(), null))
            .Returns(files.ToAsyncEnumerable());

        // Reindex check fails
        _mockApiClient.Setup(c => c.CheckFilesNeedReindexAsync(It.IsAny<CheckFilesNeedReindexRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API unavailable"));

        SetupHashComputerForFiles(filePaths);
        SetupMetadataExtractor();
        SetupBatchIngest();

        _mockApiClient.Setup(c => c.UpdateLastScannedAsync(directoryId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var orchestrator = CreateOrchestrator();

        // Act
        var result = await orchestrator.IndexDirectoryAsync(directoryId, directoryPath, CancellationToken.None);

        // Assert - all files should be processed as fallback
        result.FilesProcessed.Should().Be(3);
    }

    #endregion

    #region Hierarchical Masking Integration Tests

    [Fact]
    public async Task IndexDirectoryAsync_SkipsSiblingSubdirectory_WhenParentAlreadyScanned()
    {
        // Arrange
        var parentDir = _fixture.CreateSubdirectory("photos/2023");
        var siblingDir = _fixture.CreateSubdirectory("photos/2023/vacation");

        // First, scan the parent
        _scanSession.MarkDirectoryScanned(parentDir);

        // Now try to scan a child (should be skipped due to hierarchical masking)
        var orchestrator = CreateOrchestrator();
        var result = await orchestrator.IndexDirectoryAsync(Guid.NewGuid(), siblingDir, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.FilesScanned.Should().Be(0);
        _mockFileScanner.Verify(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<ScanProgress>?>()), Times.Never);
    }

    [Fact]
    public async Task IndexDirectoryAsync_ProcessesSibling_WhenOnlyOtherBranchScanned()
    {
        // Arrange
        var dir2022 = _fixture.CreateSubdirectory("photos/2022");
        var dir2023 = _fixture.CreateSubdirectory("photos/2023");
        var file2023 = _fixture.CreateFile("photos/2023/image.jpg");
        var directoryId = Guid.NewGuid();

        // Scan 2022 first
        _scanSession.MarkDirectoryScanned(dir2022);

        // Setup for scanning 2023
        var files = CreateScannedFiles(dir2023, new[] { file2023 });
        _mockFileScanner.Setup(s => s.ScanAsync(dir2023, true, It.IsAny<CancellationToken>(), null))
            .Returns(files.ToAsyncEnumerable());

        _mockApiClient.Setup(c => c.CheckFilesNeedReindexAsync(It.IsAny<CheckFilesNeedReindexRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckFilesNeedReindexRequest req, CancellationToken _) =>
                req.Files.Select(f => new FileNeedsReindexDto
                {
                    FilePath = f.FilePath,
                    LastModifiedAt = f.ModifiedAt,
                    NeedsReindex = true
                }).ToList());

        SetupHashComputerForFiles(new[] { file2023 });
        SetupMetadataExtractor();
        SetupBatchIngest();
        _mockApiClient.Setup(c => c.UpdateLastScannedAsync(directoryId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var orchestrator = CreateOrchestrator();

        // Act - 2023 should be processed (sibling, not covered by 2022)
        var result = await orchestrator.IndexDirectoryAsync(directoryId, dir2023, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.FilesScanned.Should().Be(1);
        _mockFileScanner.Verify(s => s.ScanAsync(dir2023, true, It.IsAny<CancellationToken>(), null), Times.Once);
    }

    #endregion

    #region Helper Methods

    private List<ScannedFile> CreateScannedFiles(string directoryPath, string[] filePaths)
    {
        return filePaths.Select((path, i) => new ScannedFile
        {
            FullPath = path,
            FileName = Path.GetFileName(path),
            Extension = Path.GetExtension(path),
            FileSizeBytes = 1024 * (i + 1),
            LastModifiedUtc = DateTime.UtcNow.AddDays(-i)
        }).ToList();
    }

    private void SetupHashComputerForFiles(string[] filePaths)
    {
        var hashResults = filePaths.Select(path => new HashResult
        {
            FilePath = path,
            Hash = $"hash_{Path.GetFileName(path)}",
            Success = true
        }).ToList();

        _mockHashComputer.Setup(h => h.ComputeBatchAsync(
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()))
            .Returns(hashResults.ToAsyncEnumerable());
    }

    private void SetupMetadataExtractor()
    {
        _mockMetadataExtractor.Setup(m => m.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageMetadata());
    }

    private void SetupBatchIngest()
    {
        _mockApiClient.Setup(c => c.BatchIngestFilesAsync(
            It.IsAny<BatchIngestFilesRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((BatchIngestFilesRequest req, CancellationToken _) => new BatchOperationResponse
            {
                Succeeded = req.Files.Count,
                Failed = 0
            });
    }

    #endregion
}
