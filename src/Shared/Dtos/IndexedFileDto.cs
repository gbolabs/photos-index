namespace Shared.Dtos;

/// <summary>
/// Data transfer object for indexed file information.
/// </summary>
public record IndexedFileDto
{
    public Guid Id { get; init; }
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required string FileHash { get; init; }
    public long FileSize { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime ModifiedAt { get; init; }
    public DateTime IndexedAt { get; init; }
    public string? ThumbnailPath { get; init; }
    public bool IsDuplicate { get; init; }
    public Guid? DuplicateGroupId { get; init; }
    public DateTime? DateTaken { get; init; }
    public string? CameraMake { get; init; }
    public string? CameraModel { get; init; }
    public double? GpsLatitude { get; init; }
    public double? GpsLongitude { get; init; }
    public int? Iso { get; init; }
    public string? Aperture { get; init; }
    public string? ShutterSpeed { get; init; }
    public string? LastError { get; init; }
    public int RetryCount { get; init; }
    public bool IsHidden { get; init; }
    public string? HiddenCategory { get; init; }
    public DateTime? HiddenAt { get; init; }

    // Deletion/Archive status
    public bool IsDeleted { get; init; }
    public DateTime? DeletedAt { get; init; }
    public string? ArchivePath { get; init; }
}
