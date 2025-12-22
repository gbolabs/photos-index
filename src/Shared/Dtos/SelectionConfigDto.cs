namespace Shared.Dtos;

/// <summary>
/// Complete selection algorithm configuration.
/// </summary>
public record SelectionConfigDto
{
    /// <summary>
    /// List of path priority rules.
    /// </summary>
    public IReadOnlyList<SelectionPreferenceDto> PathPriorities { get; init; } = [];

    /// <summary>
    /// Whether to prefer files with EXIF data (+20 score).
    /// </summary>
    public bool PreferExifData { get; init; } = true;

    /// <summary>
    /// Whether to prefer deeper folder structure (+5 per level, max +25).
    /// </summary>
    public bool PreferDeeperPaths { get; init; } = true;

    /// <summary>
    /// Whether to prefer older files (+1 per month, max +12).
    /// </summary>
    public bool PreferOlderFiles { get; init; } = true;

    /// <summary>
    /// Score threshold for conflict detection. If top two scores are within this range, mark as conflict.
    /// </summary>
    public int ConflictThreshold { get; init; } = 5;
}
