using System.Text.Json;
using FluentAssertions;
using Shared.Messages;
using Xunit;

namespace Shared.Tests.Messages;

/// <summary>
/// Tests for message contract serialization and deserialization.
/// Ensures that messages can be properly serialized for message broker transport.
/// </summary>
public class MessageSerializationTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void FileDiscoveredMessage_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var indexedFileId = Guid.NewGuid();
        var scanDirectoryId = Guid.NewGuid();

        var message = new FileDiscoveredMessage
        {
            CorrelationId = correlationId,
            IndexedFileId = indexedFileId,
            ScanDirectoryId = scanDirectoryId,
            FilePath = "/photos/test.jpg",
            FileHash = "abc123def456",
            FileSize = 1024000,
            ObjectKey = "files/abc123def456"
        };

        // Act
        var json = JsonSerializer.Serialize(message, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<FileDiscoveredMessage>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.CorrelationId.Should().Be(correlationId);
        deserialized.IndexedFileId.Should().Be(indexedFileId);
        deserialized.ScanDirectoryId.Should().Be(scanDirectoryId);
        deserialized.FilePath.Should().Be("/photos/test.jpg");
        deserialized.FileHash.Should().Be("abc123def456");
        deserialized.FileSize.Should().Be(1024000);
        deserialized.ObjectKey.Should().Be("files/abc123def456");
    }

    [Fact]
    public void MetadataExtractedMessage_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var indexedFileId = Guid.NewGuid();
        var dateTaken = DateTime.UtcNow;

        var message = new MetadataExtractedMessage
        {
            CorrelationId = correlationId,
            IndexedFileId = indexedFileId,
            Success = true,
            ObjectKey = "files/abc123def456",
            Width = 1920,
            Height = 1080,
            DateTaken = dateTaken,
            CameraMake = "Canon",
            CameraModel = "EOS 5D Mark IV",
            GpsLatitude = 37.7749,
            GpsLongitude = -122.4194,
            Orientation = 1
        };

        // Act
        var json = JsonSerializer.Serialize(message, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<MetadataExtractedMessage>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.CorrelationId.Should().Be(correlationId);
        deserialized.IndexedFileId.Should().Be(indexedFileId);
        deserialized.Success.Should().BeTrue();
        deserialized.ObjectKey.Should().Be("files/abc123def456");
        deserialized.Width.Should().Be(1920);
        deserialized.Height.Should().Be(1080);
        deserialized.DateTaken.Should().BeCloseTo(dateTaken, TimeSpan.FromMilliseconds(1));
        deserialized.CameraMake.Should().Be("Canon");
        deserialized.CameraModel.Should().Be("EOS 5D Mark IV");
        deserialized.GpsLatitude.Should().Be(37.7749);
        deserialized.GpsLongitude.Should().Be(-122.4194);
        deserialized.Orientation.Should().Be(1);
    }

    [Fact]
    public void MetadataExtractedMessage_WithNullableFields_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var indexedFileId = Guid.NewGuid();

        var message = new MetadataExtractedMessage
        {
            CorrelationId = correlationId,
            IndexedFileId = indexedFileId,
            Success = true,
            ObjectKey = "files/abc123def456",
            Width = null,
            Height = null,
            DateTaken = null,
            CameraMake = null,
            CameraModel = null,
            GpsLatitude = null,
            GpsLongitude = null,
            Orientation = null
        };

        // Act
        var json = JsonSerializer.Serialize(message, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<MetadataExtractedMessage>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.CorrelationId.Should().Be(correlationId);
        deserialized.IndexedFileId.Should().Be(indexedFileId);
        deserialized.Success.Should().BeTrue();
        deserialized.ObjectKey.Should().Be("files/abc123def456");
        deserialized.Width.Should().BeNull();
        deserialized.Height.Should().BeNull();
        deserialized.DateTaken.Should().BeNull();
        deserialized.CameraMake.Should().BeNull();
        deserialized.CameraModel.Should().BeNull();
        deserialized.GpsLatitude.Should().BeNull();
        deserialized.GpsLongitude.Should().BeNull();
        deserialized.Orientation.Should().BeNull();
    }

    [Fact]
    public void ThumbnailGeneratedMessage_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var indexedFileId = Guid.NewGuid();

        var message = new ThumbnailGeneratedMessage
        {
            CorrelationId = correlationId,
            IndexedFileId = indexedFileId,
            OriginalObjectKey = "files/abc123def456",
            ThumbnailObjectKey = "thumbnails/abc123def456_thumb",
            ThumbnailWidth = 300,
            ThumbnailHeight = 200,
            ThumbnailSize = 15000
        };

        // Act
        var json = JsonSerializer.Serialize(message, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<ThumbnailGeneratedMessage>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.CorrelationId.Should().Be(correlationId);
        deserialized.IndexedFileId.Should().Be(indexedFileId);
        deserialized.OriginalObjectKey.Should().Be("files/abc123def456");
        deserialized.ThumbnailObjectKey.Should().Be("thumbnails/abc123def456_thumb");
        deserialized.ThumbnailWidth.Should().Be(300);
        deserialized.ThumbnailHeight.Should().Be(200);
        deserialized.ThumbnailSize.Should().Be(15000);
    }

    [Fact]
    public void FileDiscoveredMessage_WithDefaultValues_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var message = new FileDiscoveredMessage();

        // Act
        var json = JsonSerializer.Serialize(message, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<FileDiscoveredMessage>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.CorrelationId.Should().Be(Guid.Empty);
        deserialized.IndexedFileId.Should().Be(Guid.Empty);
        deserialized.ScanDirectoryId.Should().Be(Guid.Empty);
        deserialized.FilePath.Should().Be(string.Empty);
        deserialized.FileHash.Should().Be(string.Empty);
        deserialized.FileSize.Should().Be(0);
        deserialized.ObjectKey.Should().Be(string.Empty);
    }
}
