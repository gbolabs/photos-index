namespace Shared.Dtos;

/// <summary>
/// Statistics about indexed files and duplicates.
/// </summary>
public record FileStatisticsDto
{
    public int TotalFiles { get; init; }
    public long TotalSizeBytes { get; init; }
    public int DuplicateGroups { get; init; }
    public int DuplicateFiles { get; init; }
    public long PotentialSavingsBytes { get; init; }
    public DateTime? LastIndexedAt { get; init; }
}
