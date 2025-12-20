namespace Shared.Dtos;

/// <summary>
/// Data transfer object for scan directory configuration.
/// </summary>
public record ScanDirectoryDto
{
    public Guid Id { get; init; }
    public required string Path { get; init; }
    public bool IsEnabled { get; init; }
    public DateTime? LastScannedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public int FileCount { get; init; }
}
