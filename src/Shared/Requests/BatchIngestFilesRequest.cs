using System.ComponentModel.DataAnnotations;

namespace Shared.Requests;

/// <summary>
/// Request to batch ingest indexed files from the indexing service.
/// </summary>
public record BatchIngestFilesRequest
{
    [Required]
    public Guid ScanDirectoryId { get; init; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one file is required")]
    public required IReadOnlyList<IngestFileItem> Files { get; init; }
}

/// <summary>
/// Single file item for batch ingestion.
/// </summary>
public record IngestFileItem
{
    [Required]
    public required string FilePath { get; init; }

    [Required]
    public required string FileName { get; init; }

    [Required]
    public required string FileHash { get; init; }

    [Range(0, long.MaxValue)]
    public long FileSize { get; init; }

    [Range(1, int.MaxValue)]
    public int? Width { get; init; }

    [Range(1, int.MaxValue)]
    public int? Height { get; init; }

    public DateTime? CreatedAt { get; init; }

    public DateTime ModifiedAt { get; init; }

    public string? ThumbnailBase64 { get; init; }
}
