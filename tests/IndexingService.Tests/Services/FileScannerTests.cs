using FluentAssertions;
using IndexingService.Models;
using IndexingService.Services;
using IndexingService.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace IndexingService.Tests.Services;

public class FileScannerTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture;
    private readonly Mock<ILogger<FileScanner>> _mockLogger;
    private readonly ScannerOptions _defaultOptions;

    public FileScannerTests()
    {
        _fixture = new TempDirectoryFixture();
        _mockLogger = new Mock<ILogger<FileScanner>>();
        _defaultOptions = new ScannerOptions();
    }

    public void Dispose() => _fixture.Dispose();

    private FileScanner CreateScanner(ScannerOptions? options = null)
    {
        return new FileScanner(
            Options.Create(options ?? _defaultOptions),
            _mockLogger.Object);
    }

    [Fact]
    public async Task ScanAsync_FindsAllImageFiles()
    {
        // Arrange
        _fixture.CreateFile("photo1.jpg");
        _fixture.CreateFile("photo2.png");
        _fixture.CreateFile("photo3.gif");
        _fixture.CreateFile("document.txt"); // Should be skipped
        var scanner = CreateScanner();

        // Act
        var files = await scanner.ScanAsync(_fixture.RootPath, false, CancellationToken.None).ToListAsync();

        // Assert
        files.Should().HaveCount(3);
        files.Select(f => f.Extension.ToLower()).Should().BeEquivalentTo([".jpg", ".png", ".gif"]);
    }

    [Fact]
    public async Task ScanAsync_IncludesSubdirectories_WhenEnabled()
    {
        // Arrange
        _fixture.CreateFile("photo1.jpg");
        _fixture.CreateFile("subdir/photo2.jpg");
        _fixture.CreateFile("subdir/nested/photo3.jpg");
        var scanner = CreateScanner();

        // Act
        var files = await scanner.ScanAsync(_fixture.RootPath, true, CancellationToken.None).ToListAsync();

        // Assert
        files.Should().HaveCount(3);
    }

    [Fact]
    public async Task ScanAsync_ExcludesSubdirectories_WhenDisabled()
    {
        // Arrange
        _fixture.CreateFile("photo1.jpg");
        _fixture.CreateFile("subdir/photo2.jpg");
        var scanner = CreateScanner();

        // Act
        var files = await scanner.ScanAsync(_fixture.RootPath, false, CancellationToken.None).ToListAsync();

        // Assert
        files.Should().HaveCount(1);
        files[0].FileName.Should().Be("photo1.jpg");
    }

    [Fact]
    public async Task ScanAsync_SkipsHiddenFiles_WhenConfigured()
    {
        // Arrange
        _fixture.CreateFile("visible.jpg");
        _fixture.CreateFile(".hidden.jpg");
        var scanner = CreateScanner(new ScannerOptions { SkipHiddenFiles = true });

        // Act
        var files = await scanner.ScanAsync(_fixture.RootPath, false, CancellationToken.None).ToListAsync();

        // Assert
        files.Should().HaveCount(1);
        files[0].FileName.Should().Be("visible.jpg");
    }

    [Fact]
    public async Task ScanAsync_IncludesHiddenFiles_WhenConfigured()
    {
        // Arrange
        _fixture.CreateFile("visible.jpg");
        _fixture.CreateFile(".hidden.jpg");
        var scanner = CreateScanner(new ScannerOptions { SkipHiddenFiles = false });

        // Act
        var files = await scanner.ScanAsync(_fixture.RootPath, false, CancellationToken.None).ToListAsync();

        // Assert
        files.Should().HaveCount(2);
    }

    [Fact]
    public async Task ScanAsync_SkipsHiddenDirectories_WhenConfigured()
    {
        // Arrange
        _fixture.CreateFile("visible/photo.jpg");
        _fixture.CreateFile(".hidden/photo.jpg");
        var scanner = CreateScanner(new ScannerOptions { SkipHiddenDirectories = true });

        // Act
        var files = await scanner.ScanAsync(_fixture.RootPath, true, CancellationToken.None).ToListAsync();

        // Assert
        files.Should().HaveCount(1);
        files[0].FullPath.Should().Contain("visible");
    }

    [Theory]
    [InlineData(".JPG")]
    [InlineData(".Jpg")]
    [InlineData(".jpg")]
    public async Task ScanAsync_IsCaseInsensitive_ForExtensions(string extension)
    {
        // Arrange
        _fixture.CreateFile($"photo{extension}");
        var scanner = CreateScanner();

        // Act
        var files = await scanner.ScanAsync(_fixture.RootPath, false, CancellationToken.None).ToListAsync();

        // Assert
        files.Should().HaveCount(1);
    }

    [Fact]
    public async Task ScanAsync_ReturnsCorrectFileInfo()
    {
        // Arrange
        var content = new byte[1024];
        _fixture.CreateFile("photo.jpg", content);
        var scanner = CreateScanner();

        // Act
        var files = await scanner.ScanAsync(_fixture.RootPath, false, CancellationToken.None).ToListAsync();

        // Assert
        files.Should().HaveCount(1);
        var file = files[0];
        file.FileName.Should().Be("photo.jpg");
        file.Extension.Should().Be(".jpg");
        file.FileSizeBytes.Should().Be(1024);
        file.LastModifiedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ScanAsync_ReportsProgress()
    {
        // Arrange
        _fixture.CreateFile("photo1.jpg");
        _fixture.CreateFile("photo2.jpg");
        _fixture.CreateFile("photo3.jpg");
        var scanner = CreateScanner();
        var progressReports = new List<ScanProgress>();
        var progress = new Progress<ScanProgress>(p => progressReports.Add(p));

        // Act
        await scanner.ScanAsync(_fixture.RootPath, false, CancellationToken.None, progress).ToListAsync();

        // Assert
        progressReports.Should().NotBeEmpty();
        progressReports.Last().FilesFound.Should().Be(3);
    }

    [Fact]
    public async Task ScanAsync_SupportsCancellation()
    {
        // Arrange
        for (int i = 0; i < 100; i++)
        {
            _fixture.CreateFile($"photo{i}.jpg");
        }
        var scanner = CreateScanner();
        using var cts = new CancellationTokenSource();
        var filesScanned = 0;

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var file in scanner.ScanAsync(_fixture.RootPath, false, cts.Token))
            {
                filesScanned++;
                if (filesScanned >= 5)
                {
                    cts.Cancel();
                }
            }
        });

        filesScanned.Should().BeLessThan(100);
    }

    [Fact]
    public async Task ScanAsync_ReturnsEmpty_ForNonExistentDirectory()
    {
        // Arrange
        var scanner = CreateScanner();
        var nonExistentPath = Path.Combine(_fixture.RootPath, "does-not-exist");

        // Act
        var files = await scanner.ScanAsync(nonExistentPath, false, CancellationToken.None).ToListAsync();

        // Assert
        files.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_SupportsAllConfiguredExtensions()
    {
        // Arrange
        var extensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".heic", ".webp", ".bmp", ".tiff" };
        foreach (var ext in extensions)
        {
            _fixture.CreateFile($"photo{ext}");
        }
        var scanner = CreateScanner();

        // Act
        var files = await scanner.ScanAsync(_fixture.RootPath, false, CancellationToken.None).ToListAsync();

        // Assert
        files.Should().HaveCount(extensions.Length);
    }

    [Fact]
    public async Task ScanAsync_IgnoresUnsupportedExtensions()
    {
        // Arrange
        _fixture.CreateFile("photo.jpg");
        _fixture.CreateFile("document.pdf");
        _fixture.CreateFile("video.mp4");
        _fixture.CreateFile("archive.zip");
        var scanner = CreateScanner();

        // Act
        var files = await scanner.ScanAsync(_fixture.RootPath, false, CancellationToken.None).ToListAsync();

        // Assert
        files.Should().HaveCount(1);
        files[0].Extension.Should().Be(".jpg");
    }

    [Fact]
    public async Task ScanAsync_RespectsMaxDepth()
    {
        // Arrange
        _fixture.CreateFile("level1/photo1.jpg");
        _fixture.CreateFile("level1/level2/photo2.jpg");
        _fixture.CreateFile("level1/level2/level3/photo3.jpg");
        var scanner = CreateScanner(new ScannerOptions { MaxDepth = 2 });

        // Act
        var files = await scanner.ScanAsync(_fixture.RootPath, true, CancellationToken.None).ToListAsync();

        // Assert
        files.Should().HaveCount(2); // level1 and level2 only
    }

    [Fact]
    public async Task ScanAsync_SkipsZeroByteFiles()
    {
        // Arrange
        _fixture.CreateFile("valid.jpg", new byte[1024]);
        _fixture.CreateFile("empty.jpg", new byte[0]); // 0-byte file
        _fixture.CreateFile("another-valid.png", new byte[512]);
        var scanner = CreateScanner();

        // Act
        var files = await scanner.ScanAsync(_fixture.RootPath, false, CancellationToken.None).ToListAsync();

        // Assert
        files.Should().HaveCount(2); // Should skip the 0-byte file
        files.Should().OnlyContain(f => f.FileSizeBytes > 0);
        files.Should().NotContain(f => f.FileName == "empty.jpg");
    }
}
