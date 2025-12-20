namespace Shared.Requests;

/// <summary>
/// Request to auto-select original files in duplicate groups.
/// </summary>
public record AutoSelectRequest
{
    public AutoSelectStrategy Strategy { get; init; } = AutoSelectStrategy.EarliestDate;

    public IReadOnlyList<string>? PreferredDirectoryPatterns { get; init; }
}

public enum AutoSelectStrategy
{
    EarliestDate,
    ShortestPath,
    PreferredDirectory,
    LargestFile
}
