using System.Globalization;
using IndexingService.Models;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;

namespace IndexingService.Services;

/// <summary>
/// Extracts image metadata and generates thumbnails using ImageSharp.
/// </summary>
public class MetadataExtractor : IMetadataExtractor
{
    private readonly ILogger<MetadataExtractor> _logger;

    public MetadataExtractor(ILogger<MetadataExtractor> logger)
    {
        _logger = logger;
    }

    public async Task<ImageMetadata> ExtractAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return ImageMetadata.Failed("File not found");
        }

        try
        {
            using var image = await Image.LoadAsync(filePath, cancellationToken);
            var exif = image.Metadata.ExifProfile;

            return new ImageMetadata
            {
                Width = image.Width,
                Height = image.Height,
                DateTaken = ExtractDateTaken(exif, filePath),
                CameraMake = GetExifString(exif, ExifTag.Make),
                CameraModel = GetExifString(exif, ExifTag.Model),
                Latitude = ExtractLatitude(exif),
                Longitude = ExtractLongitude(exif),
                Orientation = GetExifInt(exif, ExifTag.Orientation),
                Success = true
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnknownImageFormatException ex)
        {
            _logger.LogWarning("Unknown image format: {FilePath} - {Message}", filePath, ex.Message);
            return ImageMetadata.Failed("Unknown image format");
        }
        catch (InvalidImageContentException ex)
        {
            _logger.LogWarning("Invalid image content: {FilePath} - {Message}", filePath, ex.Message);
            return ImageMetadata.Failed("Invalid image content");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract metadata: {FilePath}", filePath);
            return ImageMetadata.Failed($"Extraction failed: {ex.Message}");
        }
    }

    public async Task<byte[]?> GenerateThumbnailAsync(
        string filePath,
        ThumbnailOptions options,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Cannot generate thumbnail, file not found: {FilePath}", filePath);
            return null;
        }

        try
        {
            using var image = await Image.LoadAsync(filePath, cancellationToken);

            // Auto-orient based on EXIF data
            image.Mutate(x => x.AutoOrient());

            // Only resize if image is larger than target
            if (image.Width > options.MaxWidth || image.Height > options.MaxHeight)
            {
                var resizeOptions = new ResizeOptions
                {
                    Size = new Size(options.MaxWidth, options.MaxHeight),
                    Mode = options.PreserveAspectRatio ? ResizeMode.Max : ResizeMode.Stretch
                };
                image.Mutate(x => x.Resize(resizeOptions));
            }

            // Encode as JPEG
            using var ms = new MemoryStream();
            var encoder = new JpegEncoder { Quality = options.Quality };
            await image.SaveAsync(ms, encoder, cancellationToken);

            return ms.ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate thumbnail: {FilePath}", filePath);
            return null;
        }
    }

    private DateTime? ExtractDateTaken(ExifProfile? exif, string filePath)
    {
        if (exif is null)
            return GetFileFallbackDate(filePath);

        // Try DateTimeOriginal first (when photo was taken)
        if (exif.TryGetValue(ExifTag.DateTimeOriginal, out var dateOriginal) && dateOriginal?.Value is not null)
        {
            if (TryParseExifDate(dateOriginal.Value, out var date))
                return date;
        }

        // Fall back to DateTimeDigitized
        if (exif.TryGetValue(ExifTag.DateTimeDigitized, out var dateDigitized) && dateDigitized?.Value is not null)
        {
            if (TryParseExifDate(dateDigitized.Value, out var date))
                return date;
        }

        // Fall back to DateTime
        if (exif.TryGetValue(ExifTag.DateTime, out var dateTime) && dateTime?.Value is not null)
        {
            if (TryParseExifDate(dateTime.Value, out var date))
                return date;
        }

        return GetFileFallbackDate(filePath);
    }

    private static DateTime? GetFileFallbackDate(string filePath)
    {
        try
        {
            return File.GetLastWriteTimeUtc(filePath);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryParseExifDate(string value, out DateTime date)
    {
        // EXIF date format: "YYYY:MM:DD HH:MM:SS"
        var formats = new[]
        {
            "yyyy:MM:dd HH:mm:ss",
            "yyyy:MM:dd",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss"
        };

        return DateTime.TryParseExact(
            value,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out date);
    }

    private static string? GetExifString(ExifProfile? exif, ExifTag<string> tag)
    {
        if (exif is null)
            return null;

        return exif.TryGetValue(tag, out var value) ? value?.Value?.Trim() : null;
    }

    private static int? GetExifInt(ExifProfile? exif, ExifTag<ushort> tag)
    {
        if (exif is null)
            return null;

        return exif.TryGetValue(tag, out var value) ? value?.Value : null;
    }

    private static double? ExtractLatitude(ExifProfile? exif)
    {
        if (exif is null)
            return null;

        if (!exif.TryGetValue(ExifTag.GPSLatitude, out var latValue) || latValue?.Value is null)
            return null;

        if (!exif.TryGetValue(ExifTag.GPSLatitudeRef, out var latRef))
            return null;

        var latitude = ConvertGpsCoordinate(latValue.Value);
        if (latitude.HasValue && latRef?.Value == "S")
            latitude = -latitude;

        return latitude;
    }

    private static double? ExtractLongitude(ExifProfile? exif)
    {
        if (exif is null)
            return null;

        if (!exif.TryGetValue(ExifTag.GPSLongitude, out var lonValue) || lonValue?.Value is null)
            return null;

        if (!exif.TryGetValue(ExifTag.GPSLongitudeRef, out var lonRef))
            return null;

        var longitude = ConvertGpsCoordinate(lonValue.Value);
        if (longitude.HasValue && lonRef?.Value == "W")
            longitude = -longitude;

        return longitude;
    }

    private static double? ConvertGpsCoordinate(Rational[] values)
    {
        if (values.Length != 3)
            return null;

        try
        {
            var degrees = values[0].ToDouble();
            var minutes = values[1].ToDouble();
            var seconds = values[2].ToDouble();

            return degrees + (minutes / 60.0) + (seconds / 3600.0);
        }
        catch
        {
            return null;
        }
    }
}
