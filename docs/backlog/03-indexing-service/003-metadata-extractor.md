# 003: Metadata Extractor

**Priority**: P1 (Core Features)
**Agent**: A2
**Branch**: `feature/indexing-metadata-extractor`
**Estimated Complexity**: Medium

## Objective

Extract image metadata including dimensions, EXIF data (date taken, camera info), and generate thumbnails using ImageSharp.

## Dependencies

- `03-indexing-service/001-file-scanner.md`
- `03-indexing-service/002-hash-computer.md`

## Acceptance Criteria

- [ ] Extract image dimensions (width, height)
- [ ] Extract EXIF date taken (fall back to file date)
- [ ] Extract camera make/model if available
- [ ] Extract GPS coordinates if available
- [ ] Generate thumbnail (configurable size, default 200x200)
- [ ] Handle corrupted/invalid images gracefully
- [ ] Support all configured image formats
- [ ] Memory-efficient processing

## TDD Steps

### Red Phase - Metadata Extraction
```csharp
// tests/IndexingService.Tests/MetadataExtractorTests.cs
public class MetadataExtractorTests
{
    [Fact]
    public async Task ExtractMetadata_ValidJpeg_ReturnsAllMetadata()
    {
        // Arrange
        var testImage = GetTestResourcePath("sample.jpg");
        var extractor = new MetadataExtractor();

        // Act
        var result = await extractor.ExtractAsync(testImage, CancellationToken.None);

        // Assert
        result.Width.Should().Be(4032);
        result.Height.Should().Be(3024);
        result.DateTaken.Should().NotBeNull();
    }

    [Fact]
    public async Task ExtractMetadata_NoExifDate_FallsBackToFileDate()
    {
        // Test with image that has no EXIF
    }
}
```

### Red Phase - Thumbnail Generation
```csharp
[Fact]
public async Task GenerateThumbnail_CreatesCorrectSize()
{
    // Assert thumbnail is within bounds
}

[Fact]
public async Task GenerateThumbnail_MaintainsAspectRatio()
{
    // Assert aspect ratio preserved
}
```

### Red Phase - Error Handling
```csharp
[Fact]
public async Task ExtractMetadata_CorruptedImage_ReturnsPartialResult()
{
    // Should return what it can, mark as failed
}

[Fact]
public async Task ExtractMetadata_UnsupportedFormat_ReturnsError()
{
    // Test with .txt file renamed to .jpg
}
```

### Green Phase
Implement using SixLabors.ImageSharp.

### Refactor Phase
Optimize memory usage, add caching.

## Files to Create/Modify

```
src/IndexingService/
├── Services/
│   ├── IMetadataExtractor.cs
│   └── MetadataExtractor.cs
├── Models/
│   ├── ImageMetadata.cs
│   └── ThumbnailOptions.cs
└── appsettings.json (add thumbnail options)

tests/IndexingService.Tests/
├── Services/
│   └── MetadataExtractorTests.cs
└── TestResources/
    ├── sample.jpg
    ├── sample-no-exif.png
    └── corrupted.jpg
```

## Service Implementation

```csharp
public interface IMetadataExtractor
{
    Task<ImageMetadata> ExtractAsync(string filePath, CancellationToken cancellationToken);
    Task<byte[]?> GenerateThumbnailAsync(string filePath, ThumbnailOptions options, CancellationToken cancellationToken);
}

public record ImageMetadata
{
    public int? Width { get; init; }
    public int? Height { get; init; }
    public DateTime? DateTaken { get; init; }
    public string? CameraMake { get; init; }
    public string? CameraModel { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public ImageOrientation? Orientation { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
}

public record ThumbnailOptions
{
    public int MaxWidth { get; init; } = 200;
    public int MaxHeight { get; init; } = 200;
    public int Quality { get; init; } = 75;
    public bool PreserveAspectRatio { get; init; } = true;
}
```

## ImageSharp Implementation

```csharp
public async Task<ImageMetadata> ExtractAsync(string filePath, CancellationToken cancellationToken)
{
    try
    {
        using var image = await Image.LoadAsync(filePath, cancellationToken);
        var metadata = image.Metadata;
        var exif = metadata.ExifProfile;

        DateTime? dateTaken = null;
        if (exif?.TryGetValue(ExifTag.DateTimeOriginal, out var dateValue) == true)
        {
            dateTaken = ParseExifDate(dateValue.Value);
        }

        return new ImageMetadata
        {
            Width = image.Width,
            Height = image.Height,
            DateTaken = dateTaken ?? File.GetLastWriteTimeUtc(filePath),
            CameraMake = exif?.GetValue(ExifTag.Make)?.Value,
            CameraModel = exif?.GetValue(ExifTag.Model)?.Value,
            // GPS extraction...
            Success = true
        };
    }
    catch (Exception ex)
    {
        return new ImageMetadata { Success = false, Error = ex.Message };
    }
}

public async Task<byte[]?> GenerateThumbnailAsync(string filePath, ThumbnailOptions options, CancellationToken cancellationToken)
{
    using var image = await Image.LoadAsync(filePath, cancellationToken);

    image.Mutate(x => x.AutoOrient());
    image.Mutate(x => x.Resize(new ResizeOptions
    {
        Size = new Size(options.MaxWidth, options.MaxHeight),
        Mode = ResizeMode.Max
    }));

    using var ms = new MemoryStream();
    await image.SaveAsJpegAsync(ms, new JpegEncoder { Quality = options.Quality }, cancellationToken);
    return ms.ToArray();
}
```

## Configuration

```json
{
  "Thumbnail": {
    "MaxWidth": 200,
    "MaxHeight": 200,
    "Quality": 75,
    "GenerateThumbnails": true
  }
}
```

## Test Resources

Include sample images in test project:
- JPEG with full EXIF data
- PNG without EXIF
- HEIC file
- Rotated image (test auto-orient)
- Corrupted image file

## Test Coverage

- Metadata extraction: 90% minimum
- Thumbnail generation: 85% minimum
- Error handling: 100%

## Completion Checklist

- [ ] Create IMetadataExtractor interface
- [ ] Create ImageMetadata and ThumbnailOptions models
- [ ] Implement metadata extraction with ImageSharp
- [ ] Implement EXIF date parsing with fallback
- [ ] Implement thumbnail generation with auto-orient
- [ ] Add GPS coordinate extraction
- [ ] Handle corrupted images gracefully
- [ ] Add test resource images to project
- [ ] Write comprehensive unit tests
- [ ] Add configuration options
- [ ] Register in DI container
- [ ] All tests passing with coverage met
- [ ] PR created and reviewed
