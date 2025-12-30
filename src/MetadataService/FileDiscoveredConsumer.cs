using MassTransit;
using Shared.Messages;
using Shared.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace MetadataService;

public class FileDiscoveredConsumer : IConsumer<FileDiscoveredMessage>
{
    private readonly IObjectStorage _objectStorage;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<FileDiscoveredConsumer> _logger;
    private readonly IConfiguration _configuration;

    public FileDiscoveredConsumer(
        IObjectStorage objectStorage,
        IPublishEndpoint publishEndpoint,
        ILogger<FileDiscoveredConsumer> logger,
        IConfiguration configuration)
    {
        _objectStorage = objectStorage;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task Consume(ConsumeContext<FileDiscoveredMessage> context)
    {
        var message = context.Message;
        var ct = context.CancellationToken;

        _logger.LogInformation(
            "Processing metadata for file {FileId}, CorrelationId: {CorrelationId}",
            message.IndexedFileId,
            message.CorrelationId);

        var bucket = _configuration["Minio:ImagesBucket"] ?? "images";
        var objectKey = message.MetadataObjectKey;

        try
        {
            await using var imageStream = await _objectStorage.DownloadAsync(bucket, objectKey, ct);

            using var image = await Image.LoadAsync(imageStream, ct);

            var result = new MetadataExtractedMessage
            {
                CorrelationId = message.CorrelationId,
                IndexedFileId = message.IndexedFileId,
                Success = true,
                Width = image.Width,
                Height = image.Height,
                DateTaken = ExtractDateTaken(image),
                CameraMake = ExtractExifString(image, ExifTag.Make),
                CameraModel = ExtractExifString(image, ExifTag.Model),
                GpsLatitude = ExtractGpsCoordinate(image, true),
                GpsLongitude = ExtractGpsCoordinate(image, false),
                Iso = ExtractIso(image),
                Aperture = ExtractAperture(image),
                ShutterSpeed = ExtractShutterSpeed(image)
            };

            await _publishEndpoint.Publish(result, ct);

            _logger.LogInformation(
                "Published MetadataExtractedMessage for file {FileId}: {Width}x{Height}",
                message.IndexedFileId,
                result.Width,
                result.Height);

            // Delete our copy from MinIO after successful processing
            await DeleteSourceFileAsync(bucket, objectKey, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract metadata for file {FileId}", message.IndexedFileId);

            var errorResult = new MetadataExtractedMessage
            {
                CorrelationId = message.CorrelationId,
                IndexedFileId = message.IndexedFileId,
                Success = false,
                ErrorMessage = ex.Message
            };

            await _publishEndpoint.Publish(errorResult, ct);

            // Delete our copy even on failure - file can't be reprocessed anyway
            await DeleteSourceFileAsync(bucket, objectKey, ct);
        }
    }

    private async Task DeleteSourceFileAsync(string bucket, string objectKey, CancellationToken ct)
    {
        try
        {
            await _objectStorage.DeleteAsync(bucket, objectKey, ct);
            _logger.LogInformation("Deleted source file from MinIO: {Bucket}/{Key}", bucket, objectKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete source file {Key} - may already be deleted", objectKey);
        }
    }

    private static DateTime? ExtractDateTaken(Image image)
    {
        var exif = image.Metadata.ExifProfile;
        if (exif is null) return null;

        if (exif.TryGetValue(ExifTag.DateTimeOriginal, out var dateOriginal))
        {
            if (TryParseExifDate(dateOriginal?.Value, out var result))
                return result;
        }

        if (exif.TryGetValue(ExifTag.DateTime, out var dateTime))
        {
            if (TryParseExifDate(dateTime?.Value, out var result))
                return result;
        }

        return null;
    }

    private static bool TryParseExifDate(string? value, out DateTime result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        // Skip invalid placeholder values
        if (value.StartsWith("0000:") || value.Trim() == "")
            return false;

        // Common EXIF date formats
        var formats = new[]
        {
            "yyyy:MM:dd HH:mm:ss",       // Standard EXIF format
            "yyyy:MM:dd HH:mm:ss.fff",   // With milliseconds
            "yyyy:MM:dd",                 // Date only
            "yyyy-MM-dd HH:mm:ss",       // ISO-like
            "yyyy-MM-ddTHH:mm:ss",       // ISO 8601
            "yyyy-MM-ddTHH:mm:sszzz",    // ISO 8601 with timezone
            "yyyy:MM:dd HH:mm:sszzz",    // EXIF with timezone
        };

        // Use AssumeUniversal to ensure the DateTime has Kind=Utc for PostgreSQL compatibility
        if (DateTime.TryParseExact(value, formats,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out result))
        {
            return true;
        }

        // Fallback to general parsing
        if (DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out result))
        {
            return true;
        }

        return false;
    }

    private static string? ExtractExifString(Image image, ExifTag<string> tag)
    {
        var exif = image.Metadata.ExifProfile;
        if (exif is null) return null;

        return exif.TryGetValue(tag, out var value) ? value?.Value?.Trim() : null;
    }

    private static double? ExtractGpsCoordinate(Image image, bool isLatitude)
    {
        var exif = image.Metadata.ExifProfile;
        if (exif is null) return null;

        var coordTag = isLatitude ? ExifTag.GPSLatitude : ExifTag.GPSLongitude;
        var refTag = isLatitude ? ExifTag.GPSLatitudeRef : ExifTag.GPSLongitudeRef;

        if (!exif.TryGetValue(coordTag, out var coordValue) || coordValue?.Value is null)
            return null;

        var rationals = coordValue.Value;
        if (rationals.Length != 3) return null;

        var degrees = (double)rationals[0].Numerator / rationals[0].Denominator;
        var minutes = (double)rationals[1].Numerator / rationals[1].Denominator;
        var seconds = (double)rationals[2].Numerator / rationals[2].Denominator;

        var coordinate = degrees + (minutes / 60) + (seconds / 3600);

        if (exif.TryGetValue(refTag, out var refValue))
        {
            var reference = refValue?.Value;
            if (reference == "S" || reference == "W")
                coordinate = -coordinate;
        }

        return coordinate;
    }

    private static int? ExtractIso(Image image)
    {
        var exif = image.Metadata.ExifProfile;
        if (exif is null) return null;

        if (exif.TryGetValue(ExifTag.ISOSpeedRatings, out var isoValue) && isoValue?.Value is not null)
        {
            var values = isoValue.Value;
            return values.Length > 0 ? (int?)values[0] : null;
        }

        return null;
    }

    private static string? ExtractAperture(Image image)
    {
        var exif = image.Metadata.ExifProfile;
        if (exif is null) return null;

        if (exif.TryGetValue(ExifTag.FNumber, out var fnumber) && fnumber?.Value is not null)
        {
            var value = fnumber.Value;
            var aperture = (double)value.Numerator / value.Denominator;
            return $"f/{aperture:0.#}";
        }

        return null;
    }

    private static string? ExtractShutterSpeed(Image image)
    {
        var exif = image.Metadata.ExifProfile;
        if (exif is null) return null;

        if (exif.TryGetValue(ExifTag.ExposureTime, out var exposure) && exposure?.Value is not null)
        {
            var value = exposure.Value;
            if (value.Numerator == 1)
            {
                return $"1/{value.Denominator}";
            }
            else
            {
                var seconds = (double)value.Numerator / value.Denominator;
                if (seconds >= 1)
                    return $"{seconds:0.#}s";
                return $"1/{(int)(1 / seconds)}";
            }
        }

        return null;
    }
}
