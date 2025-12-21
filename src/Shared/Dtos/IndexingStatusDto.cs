namespace Shared.Dtos;

/// <summary>
/// Data transfer object for indexing status information.
/// </summary>
public record IndexingStatusDto
{
    public bool IsRunning { get; init; }
    public Guid? CurrentDirectoryId { get; init; }
    public string? CurrentDirectoryPath { get; init; }
    public int FilesScanned { get; init; }
    public int FilesIngested { get; init; }
    public int FilesFailed { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? LastUpdatedAt { get; init; }
}
