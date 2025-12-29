using CleanerService.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Shared.Dtos;
using Xunit;

namespace CleanerService.Tests;

/// <summary>
/// Tests for the CleanerService options and configuration.
/// </summary>
public class CleanerServiceOptionsTests
{
    [Fact]
    public void CleanerServiceOptions_DefaultValues()
    {
        // Arrange & Act
        var options = new CleanerServiceOptions();

        // Assert
        options.DryRunEnabled.Should().BeTrue("Dry-run should be enabled by default for safety");
        options.ApiBaseUrl.Should().Be("http://localhost:5000", "Default localhost URL for development");
        options.MaxConcurrency.Should().Be(4);
        options.UploadTimeoutSeconds.Should().Be(300);
    }

    [Fact]
    public void CleanerServiceOptions_CanBeConfigured()
    {
        // Arrange
        var options = new CleanerServiceOptions
        {
            ApiBaseUrl = "https://api.example.com",
            DryRunEnabled = false
        };

        // Assert
        options.ApiBaseUrl.Should().Be("https://api.example.com");
        options.DryRunEnabled.Should().BeFalse();
    }
}

/// <summary>
/// Tests for content type detection.
/// </summary>
public class ContentTypeTests
{
    [Theory]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".jpeg", "image/jpeg")]
    [InlineData(".JPG", "image/jpeg")]
    [InlineData(".png", "image/png")]
    [InlineData(".PNG", "image/png")]
    [InlineData(".gif", "image/gif")]
    [InlineData(".heic", "image/heic")]
    [InlineData(".heif", "image/heif")]
    [InlineData(".webp", "image/webp")]
    [InlineData(".avif", "image/avif")]
    [InlineData(".bmp", "image/bmp")]
    [InlineData(".tiff", "image/tiff")]
    [InlineData(".tif", "image/tiff")]
    [InlineData(".xyz", "application/octet-stream")]
    [InlineData("", "application/octet-stream")]
    public void GetContentType_ReturnsCorrectType(string extension, string expectedContentType)
    {
        // Act
        var result = GetContentType($"file{extension}");

        // Assert
        result.Should().Be(expectedContentType);
    }

    // Mimics the private method in DeleteService
    private static string GetContentType(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".heic" => "image/heic",
            ".heif" => "image/heif",
            ".webp" => "image/webp",
            ".avif" => "image/avif",
            ".bmp" => "image/bmp",
            ".tiff" or ".tif" => "image/tiff",
            _ => "application/octet-stream"
        };
    }
}
