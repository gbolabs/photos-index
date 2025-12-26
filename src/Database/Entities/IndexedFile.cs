namespace Database.Entities;

public class IndexedFile
{
    public Guid Id { get; set; }

    public required string FilePath { get; set; }

    public required string FileName { get; set; }

    public long FileSize { get; set; }

    public required string FileHash { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public DateTime IndexedAt { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public string? ThumbnailPath { get; set; }

    public bool IsDuplicate { get; set; }

    public Guid? DuplicateGroupId { get; set; }

    // Extended EXIF metadata
    public DateTime? DateTaken { get; set; }

    public string? CameraMake { get; set; }

    public string? CameraModel { get; set; }

    public double? GpsLatitude { get; set; }

    public double? GpsLongitude { get; set; }

    public int? Iso { get; set; }

    public string? Aperture { get; set; }  // e.g., "f/2.8"

    public string? ShutterSpeed { get; set; }  // e.g., "1/125"

    // Processing timestamps for tracking metadata/thumbnail completion
    public DateTime? MetadataProcessedAt { get; set; }

    public DateTime? ThumbnailProcessedAt { get; set; }

    // Retry tracking for failed files
    public string? LastError { get; set; }

    public int RetryCount { get; set; } = 0;

    // Navigation property
    public DuplicateGroup? DuplicateGroup { get; set; }
}
