namespace Shared.Dtos;

/// <summary>
/// Data transfer object for duplicate file groups.
/// </summary>
public record DuplicateGroupDto
{
    public Guid Id { get; init; }
    public required string Hash { get; init; }
    public int FileCount { get; init; }
    public long TotalSize { get; init; }
    public long PotentialSavings => FileCount > 1 ? TotalSize - (TotalSize / FileCount) : 0;
    public DateTime? ResolvedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public Guid? OriginalFileId { get; init; }
    public IReadOnlyList<IndexedFileDto> Files { get; init; } = [];

    /// <summary>
    /// Thumbnail path of the first file in the group (for list display).
    /// </summary>
    public string? FirstFileThumbnailPath { get; init; }

    // Validation fields
    public string Status { get; init; } = "pending";
    public DateTime? ValidatedAt { get; init; }
    public Guid? KeptFileId { get; init; }

    // Review session fields
    public DateTime? LastReviewedAt { get; init; }
    public int? ReviewOrder { get; init; }
    public Guid? ReviewSessionId { get; init; }
}
