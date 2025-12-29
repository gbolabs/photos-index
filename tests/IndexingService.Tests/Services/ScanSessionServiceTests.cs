using FluentAssertions;
using IndexingService.Services;
using Xunit;

namespace IndexingService.Tests.Services;

public class ScanSessionServiceTests
{
    private readonly ScanSessionService _sut;

    public ScanSessionServiceTests()
    {
        _sut = new ScanSessionService();
    }

    #region Session Lifecycle Tests

    [Fact]
    public void SessionId_IsInitialized_OnConstruction()
    {
        // Assert
        _sut.SessionId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void SessionStartTime_IsInitialized_OnConstruction()
    {
        // Assert
        _sut.SessionStartTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void StartNewSession_GeneratesNewSessionId()
    {
        // Arrange
        var originalSessionId = _sut.SessionId;

        // Act
        _sut.StartNewSession();

        // Assert
        _sut.SessionId.Should().NotBe(originalSessionId);
        _sut.SessionId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void StartNewSession_UpdatesSessionStartTime()
    {
        // Arrange
        var originalStartTime = _sut.SessionStartTime;

        // Act
        Thread.Sleep(10); // Small delay to ensure time difference
        _sut.StartNewSession();

        // Assert
        _sut.SessionStartTime.Should().BeAfter(originalStartTime);
    }

    [Fact]
    public void StartNewSession_ClearsAllTrackingData()
    {
        // Arrange
        _sut.MarkDirectoryScanned("/path/to/dir");
        _sut.MarkFileProcessed("/path/to/file.jpg");

        // Act
        _sut.StartNewSession();

        // Assert
        _sut.ProcessedFileCount.Should().Be(0);
        _sut.ScannedDirectoryCount.Should().Be(0);
        _sut.IsDirectoryScanned("/path/to/dir").Should().BeFalse();
        _sut.IsFileProcessed("/path/to/file.jpg").Should().BeFalse();
    }

    #endregion

    #region Directory Tracking Tests

    [Fact]
    public void MarkDirectoryScanned_TracksDirectory()
    {
        // Arrange
        var path = "/photos/2023";

        // Act
        _sut.MarkDirectoryScanned(path);

        // Assert
        _sut.IsDirectoryScanned(path).Should().BeTrue();
        _sut.ScannedDirectoryCount.Should().Be(1);
    }

    [Fact]
    public void IsDirectoryScanned_ReturnsFalse_ForUnscannedDirectory()
    {
        // Arrange
        _sut.MarkDirectoryScanned("/photos/2023");

        // Act & Assert
        _sut.IsDirectoryScanned("/photos/2024").Should().BeFalse();
    }

    [Fact]
    public void MarkDirectoryScanned_HandlesMultipleDirectories()
    {
        // Arrange & Act
        _sut.MarkDirectoryScanned("/photos/2022");
        _sut.MarkDirectoryScanned("/photos/2023");
        _sut.MarkDirectoryScanned("/photos/2024");

        // Assert
        _sut.ScannedDirectoryCount.Should().Be(3);
        _sut.IsDirectoryScanned("/photos/2022").Should().BeTrue();
        _sut.IsDirectoryScanned("/photos/2023").Should().BeTrue();
        _sut.IsDirectoryScanned("/photos/2024").Should().BeTrue();
    }

    [Fact]
    public void MarkDirectoryScanned_IsCaseInsensitive()
    {
        // Arrange
        _sut.MarkDirectoryScanned("/Photos/2023");

        // Assert
        _sut.IsDirectoryScanned("/photos/2023").Should().BeTrue();
        _sut.IsDirectoryScanned("/PHOTOS/2023").Should().BeTrue();
    }

    [Fact]
    public void MarkDirectoryScanned_NormalizesPaths()
    {
        // Arrange - trailing separator should be normalized
        _sut.MarkDirectoryScanned("/photos/2023/");

        // Assert
        _sut.IsDirectoryScanned("/photos/2023").Should().BeTrue();
    }

    #endregion

    #region File Tracking Tests

    [Fact]
    public void MarkFileProcessed_TracksFile()
    {
        // Arrange
        var path = "/photos/2023/image.jpg";

        // Act
        _sut.MarkFileProcessed(path);

        // Assert
        _sut.IsFileProcessed(path).Should().BeTrue();
        _sut.ProcessedFileCount.Should().Be(1);
    }

    [Fact]
    public void IsFileProcessed_ReturnsFalse_ForUnprocessedFile()
    {
        // Arrange
        _sut.MarkFileProcessed("/photos/2023/image1.jpg");

        // Act & Assert
        _sut.IsFileProcessed("/photos/2023/image2.jpg").Should().BeFalse();
    }

    [Fact]
    public void MarkFileProcessed_IsCaseInsensitive()
    {
        // Arrange
        _sut.MarkFileProcessed("/Photos/2023/Image.JPG");

        // Assert
        _sut.IsFileProcessed("/photos/2023/image.jpg").Should().BeTrue();
        _sut.IsFileProcessed("/PHOTOS/2023/IMAGE.JPG").Should().BeTrue();
    }

    [Fact]
    public void MarkFileProcessed_HandlesMultipleFiles()
    {
        // Arrange & Act
        for (int i = 0; i < 100; i++)
        {
            _sut.MarkFileProcessed($"/photos/image{i}.jpg");
        }

        // Assert
        _sut.ProcessedFileCount.Should().Be(100);
        _sut.IsFileProcessed("/photos/image50.jpg").Should().BeTrue();
    }

    #endregion

    #region Hierarchical Masking Tests

    [Fact]
    public void IsPathCoveredByScannedDirectory_ReturnsFalse_WhenNoDirectoriesScanned()
    {
        // Act & Assert
        _sut.IsPathCoveredByScannedDirectory("/photos/2023/vacation").Should().BeFalse();
    }

    [Fact]
    public void IsPathCoveredByScannedDirectory_ReturnsTrue_ForExactMatch()
    {
        // Arrange
        _sut.MarkDirectoryScanned("/photos/2023");

        // Act & Assert
        _sut.IsPathCoveredByScannedDirectory("/photos/2023").Should().BeTrue();
    }

    [Fact]
    public void IsPathCoveredByScannedDirectory_ReturnsTrue_ForSubdirectory()
    {
        // Arrange - marking parent directory as scanned
        _sut.MarkDirectoryScanned("/photos");

        // Act & Assert - subdirectories should be covered
        _sut.IsPathCoveredByScannedDirectory("/photos/2023").Should().BeTrue();
        _sut.IsPathCoveredByScannedDirectory("/photos/2023/vacation").Should().BeTrue();
        _sut.IsPathCoveredByScannedDirectory("/photos/2023/vacation/day1").Should().BeTrue();
    }

    [Fact]
    public void IsPathCoveredByScannedDirectory_ReturnsFalse_ForParentDirectory()
    {
        // Arrange - marking subdirectory as scanned
        _sut.MarkDirectoryScanned("/photos/2023/vacation");

        // Act & Assert - parent directories should NOT be covered
        _sut.IsPathCoveredByScannedDirectory("/photos/2023").Should().BeFalse();
        _sut.IsPathCoveredByScannedDirectory("/photos").Should().BeFalse();
    }

    [Fact]
    public void IsPathCoveredByScannedDirectory_ReturnsFalse_ForSiblingDirectory()
    {
        // Arrange
        _sut.MarkDirectoryScanned("/photos/2023");

        // Act & Assert - sibling directories should NOT be covered
        _sut.IsPathCoveredByScannedDirectory("/photos/2024").Should().BeFalse();
        _sut.IsPathCoveredByScannedDirectory("/documents").Should().BeFalse();
    }

    [Fact]
    public void IsPathCoveredByScannedDirectory_HandlesPartialNameMatches()
    {
        // Arrange
        _sut.MarkDirectoryScanned("/photos/2023");

        // Act & Assert - paths with similar prefixes but not true subdirectories
        _sut.IsPathCoveredByScannedDirectory("/photos/2023-backup").Should().BeFalse();
        _sut.IsPathCoveredByScannedDirectory("/photos/20230101").Should().BeFalse();
    }

    [Fact]
    public void IsPathCoveredByScannedDirectory_WorksWithMultipleScannedDirectories()
    {
        // Arrange
        _sut.MarkDirectoryScanned("/photos/2022");
        _sut.MarkDirectoryScanned("/photos/2023");
        _sut.MarkDirectoryScanned("/documents");

        // Act & Assert
        _sut.IsPathCoveredByScannedDirectory("/photos/2022/vacation").Should().BeTrue();
        _sut.IsPathCoveredByScannedDirectory("/photos/2023/vacation").Should().BeTrue();
        _sut.IsPathCoveredByScannedDirectory("/documents/work").Should().BeTrue();
        _sut.IsPathCoveredByScannedDirectory("/photos/2024").Should().BeFalse();
    }

    [Fact]
    public void IsPathCoveredByScannedDirectory_IsCaseInsensitive()
    {
        // Arrange
        _sut.MarkDirectoryScanned("/Photos/2023");

        // Act & Assert
        _sut.IsPathCoveredByScannedDirectory("/photos/2023/vacation").Should().BeTrue();
        _sut.IsPathCoveredByScannedDirectory("/PHOTOS/2023/VACATION").Should().BeTrue();
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task MarkDirectoryScanned_IsThreadSafe()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 100).Select(i =>
            Task.Run(() => _sut.MarkDirectoryScanned($"/photos/dir{i}")));

        // Act
        await Task.WhenAll(tasks);

        // Assert
        _sut.ScannedDirectoryCount.Should().Be(100);
    }

    [Fact]
    public async Task MarkFileProcessed_IsThreadSafe()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 100).Select(i =>
            Task.Run(() => _sut.MarkFileProcessed($"/photos/file{i}.jpg")));

        // Act
        await Task.WhenAll(tasks);

        // Assert
        _sut.ProcessedFileCount.Should().Be(100);
    }

    [Fact]
    public async Task StartNewSession_IsThreadSafe_WithConcurrentReads()
    {
        // Arrange - populate some data
        for (int i = 0; i < 50; i++)
        {
            _sut.MarkDirectoryScanned($"/photos/dir{i}");
            _sut.MarkFileProcessed($"/photos/file{i}.jpg");
        }

        // Act - concurrent reads during session reset
        var tasks = new List<Task>();
        tasks.Add(Task.Run(() => _sut.StartNewSession()));
        tasks.AddRange(Enumerable.Range(0, 10).Select(i =>
            Task.Run(() =>
            {
                // These should not throw
                _ = _sut.IsDirectoryScanned($"/photos/dir{i}");
                _ = _sut.IsFileProcessed($"/photos/file{i}.jpg");
                _ = _sut.ProcessedFileCount;
                _ = _sut.ScannedDirectoryCount;
            })));

        // Assert - should not throw
        await Task.WhenAll(tasks);
        _sut.ProcessedFileCount.Should().Be(0);
        _sut.ScannedDirectoryCount.Should().Be(0);
    }

    #endregion

    #region Restart Simulation Tests

    [Fact]
    public void SimulatingRestart_ClearsInMemorySessionData()
    {
        // Arrange - simulate initial indexing run
        _sut.MarkDirectoryScanned("/photos/2023");
        _sut.MarkFileProcessed("/photos/2023/image1.jpg");
        _sut.MarkFileProcessed("/photos/2023/image2.jpg");

        // Act - simulate restart by creating new session
        _sut.StartNewSession();

        // Assert - all in-memory tracking is cleared
        _sut.IsDirectoryScanned("/photos/2023").Should().BeFalse();
        _sut.IsFileProcessed("/photos/2023/image1.jpg").Should().BeFalse();
        _sut.IsPathCoveredByScannedDirectory("/photos/2023/vacation").Should().BeFalse();
    }

    [Fact]
    public void MultipleSessionsIndependent_TrackingDataNotPersisted()
    {
        // Arrange - first session
        var firstSessionId = _sut.SessionId;
        _sut.MarkDirectoryScanned("/photos/2022");
        _sut.MarkFileProcessed("/photos/2022/img.jpg");

        // Act - start new session (simulates restart)
        _sut.StartNewSession();
        var secondSessionId = _sut.SessionId;

        // Assert - sessions are independent
        firstSessionId.Should().NotBe(secondSessionId);
        _sut.IsDirectoryScanned("/photos/2022").Should().BeFalse();
        _sut.IsFileProcessed("/photos/2022/img.jpg").Should().BeFalse();
    }

    #endregion

    #region Progressive Scanning Simulation Tests

    [Fact]
    public void ProgressiveScanning_MaskingUpdatesAsDirectoriesComplete()
    {
        // Simulate progressive scanning where subdirectories complete first

        // Step 1: Scan /photos/2023/january
        _sut.MarkDirectoryScanned("/photos/2023/january");
        _sut.IsPathCoveredByScannedDirectory("/photos/2023/january/day1").Should().BeTrue();
        _sut.IsPathCoveredByScannedDirectory("/photos/2023/february").Should().BeFalse();
        _sut.IsPathCoveredByScannedDirectory("/photos/2023").Should().BeFalse();

        // Step 2: Scan /photos/2023/february
        _sut.MarkDirectoryScanned("/photos/2023/february");
        _sut.IsPathCoveredByScannedDirectory("/photos/2023/february/day1").Should().BeTrue();
        _sut.IsPathCoveredByScannedDirectory("/photos/2023").Should().BeFalse(); // Parent still not scanned

        // Step 3: Now scan the parent - all children are now covered
        _sut.MarkDirectoryScanned("/photos/2023");
        _sut.IsPathCoveredByScannedDirectory("/photos/2023/march").Should().BeTrue();
        _sut.IsPathCoveredByScannedDirectory("/photos/2023/any/nested/path").Should().BeTrue();
    }

    [Fact]
    public void ProgressiveScanning_ParentMaskingOverridesChildTracking()
    {
        // When parent is marked as scanned, it covers all children even if
        // they weren't explicitly tracked

        // Arrange - only some subdirectories were explicitly tracked
        _sut.MarkDirectoryScanned("/photos/2023/january");
        _sut.MarkDirectoryScanned("/photos/2023/february");

        // Act - parent is now marked as fully scanned
        _sut.MarkDirectoryScanned("/photos");

        // Assert - any path under /photos is now covered
        _sut.IsPathCoveredByScannedDirectory("/photos/2023/march").Should().BeTrue();
        _sut.IsPathCoveredByScannedDirectory("/photos/2024").Should().BeTrue();
        _sut.IsPathCoveredByScannedDirectory("/photos/any/deep/nested/path").Should().BeTrue();
    }

    #endregion
}
