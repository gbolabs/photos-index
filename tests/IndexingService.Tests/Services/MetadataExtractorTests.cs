using FluentAssertions;
using IndexingService.Models;
using IndexingService.Services;
using IndexingService.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace IndexingService.Tests.Services;

public class MetadataExtractorTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture;
    private readonly Mock<ILogger<MetadataExtractor>> _mockLogger;
    private readonly MetadataExtractor _extractor;

    public MetadataExtractorTests()
    {
        _fixture = new TempDirectoryFixture();
        _mockLogger = new Mock<ILogger<MetadataExtractor>>();
        _extractor = new MetadataExtractor(_mockLogger.Object);
    }

    public void Dispose() => _fixture.Dispose();

    private string CreateTestImage(int width, int height, string fileName = "test.jpg")
    {
        var filePath = Path.Combine(_fixture.RootPath, fileName);
        using var image = new Image<Rgba32>(width, height);
        image.SaveAsJpeg(filePath);
        return filePath;
    }

    private string CreateTestPng(int width, int height, string fileName = "test.png")
    {
        var filePath = Path.Combine(_fixture.RootPath, fileName);
        using var image = new Image<Rgba32>(width, height);
        image.SaveAsPng(filePath);
        return filePath;
    }

    [Fact]
    public async Task ExtractAsync_ReturnsCorrectDimensions()
    {
        // Arrange
        var filePath = CreateTestImage(800, 600);

        // Act
        var result = await _extractor.ExtractAsync(filePath, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Width.Should().Be(800);
        result.Height.Should().Be(600);
    }

    [Fact]
    public async Task ExtractAsync_WorksWithPng()
    {
        // Arrange
        var filePath = CreateTestPng(1024, 768);

        // Act
        var result = await _extractor.ExtractAsync(filePath, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Width.Should().Be(1024);
        result.Height.Should().Be(768);
    }

    [Fact]
    public async Task ExtractAsync_FileNotFound_ReturnsFailedResult()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_fixture.RootPath, "does-not-exist.jpg");

        // Act
        var result = await _extractor.ExtractAsync(nonExistentPath, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ExtractAsync_InvalidImage_ReturnsFailedResult()
    {
        // Arrange
        var filePath = _fixture.CreateFile("invalid.jpg", "not an image"u8.ToArray());

        // Act
        var result = await _extractor.ExtractAsync(filePath, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExtractAsync_NoExif_ReturnsNullDateTaken()
    {
        // Arrange - Create image without EXIF data
        var filePath = CreateTestImage(100, 100);

        // Act
        var result = await _extractor.ExtractAsync(filePath, CancellationToken.None);

        // Assert - DateTaken should be null when no EXIF date exists
        // (filesystem dates are stored separately as CreatedAt/ModifiedAt)
        result.Success.Should().BeTrue();
        result.DateTaken.Should().BeNull();
    }

    [Fact]
    public async Task GenerateThumbnailAsync_CreatesCorrectSize()
    {
        // Arrange
        var filePath = CreateTestImage(1920, 1080);
        var options = new ThumbnailOptions { MaxWidth = 200, MaxHeight = 200 };

        // Act
        var thumbnailBytes = await _extractor.GenerateThumbnailAsync(filePath, options, CancellationToken.None);

        // Assert
        thumbnailBytes.Should().NotBeNull();
        using var thumbnail = Image.Load(thumbnailBytes!);
        thumbnail.Width.Should().BeLessOrEqualTo(200);
        thumbnail.Height.Should().BeLessOrEqualTo(200);
    }

    [Fact]
    public async Task GenerateThumbnailAsync_MaintainsAspectRatio()
    {
        // Arrange
        var filePath = CreateTestImage(1920, 1080); // 16:9 aspect ratio
        var options = new ThumbnailOptions { MaxWidth = 200, MaxHeight = 200, PreserveAspectRatio = true };

        // Act
        var thumbnailBytes = await _extractor.GenerateThumbnailAsync(filePath, options, CancellationToken.None);

        // Assert
        thumbnailBytes.Should().NotBeNull();
        using var thumbnail = Image.Load(thumbnailBytes!);

        // Original is 16:9, so width should be 200 and height should be ~112
        thumbnail.Width.Should().Be(200);
        thumbnail.Height.Should().BeInRange(110, 115);
    }

    [Fact]
    public async Task GenerateThumbnailAsync_FileNotFound_ReturnsNull()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_fixture.RootPath, "does-not-exist.jpg");
        var options = new ThumbnailOptions();

        // Act
        var result = await _extractor.GenerateThumbnailAsync(nonExistentPath, options, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GenerateThumbnailAsync_InvalidImage_ReturnsNull()
    {
        // Arrange
        var filePath = _fixture.CreateFile("invalid.jpg", "not an image"u8.ToArray());
        var options = new ThumbnailOptions();

        // Act
        var result = await _extractor.GenerateThumbnailAsync(filePath, options, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GenerateThumbnailAsync_RespectsQuality()
    {
        // Arrange
        var filePath = CreateTestImage(500, 500);
        var lowQuality = new ThumbnailOptions { Quality = 10 };
        var highQuality = new ThumbnailOptions { Quality = 95 };

        // Act
        var lowBytes = await _extractor.GenerateThumbnailAsync(filePath, lowQuality, CancellationToken.None);
        var highBytes = await _extractor.GenerateThumbnailAsync(filePath, highQuality, CancellationToken.None);

        // Assert
        lowBytes.Should().NotBeNull();
        highBytes.Should().NotBeNull();
        lowBytes!.Length.Should().BeLessThan(highBytes!.Length);
    }

    [Fact]
    public async Task GenerateThumbnailAsync_SmallImage_DoesNotUpscale()
    {
        // Arrange
        var filePath = CreateTestImage(50, 50);
        var options = new ThumbnailOptions { MaxWidth = 200, MaxHeight = 200 };

        // Act
        var thumbnailBytes = await _extractor.GenerateThumbnailAsync(filePath, options, CancellationToken.None);

        // Assert
        thumbnailBytes.Should().NotBeNull();
        using var thumbnail = Image.Load(thumbnailBytes!);
        thumbnail.Width.Should().Be(50);
        thumbnail.Height.Should().Be(50);
    }

    [Fact]
    public void ImageMetadata_Failed_CreatesFailedResult()
    {
        // Act
        var result = ImageMetadata.Failed("Test error");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Test error");
        result.Width.Should().BeNull();
        result.Height.Should().BeNull();
    }

    [Fact]
    public void ThumbnailOptions_HasCorrectDefaults()
    {
        // Act
        var options = new ThumbnailOptions();

        // Assert
        options.MaxWidth.Should().Be(200);
        options.MaxHeight.Should().Be(200);
        options.Quality.Should().Be(75);
        options.PreserveAspectRatio.Should().BeTrue();
    }
}
