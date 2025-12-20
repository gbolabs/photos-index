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
}
