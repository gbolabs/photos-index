namespace Shared.Dtos;

/// <summary>
/// Information about a directory pattern shared across duplicate groups.
/// A pattern is the unique set of parent directories containing duplicate files.
/// </summary>
public record DirectoryPatternDto
{
    /// <summary>
    /// Sorted list of unique parent directories in this pattern.
    /// </summary>
    public IReadOnlyList<string> Directories { get; init; } = [];

    /// <summary>
    /// Number of duplicate groups that share this exact directory pattern.
    /// </summary>
    public int MatchingGroupCount { get; init; }

    /// <summary>
    /// IDs of all groups matching this pattern.
    /// </summary>
    public IReadOnlyList<Guid> GroupIds { get; init; } = [];

    /// <summary>
    /// Hash of the sorted directory list for quick comparison.
    /// </summary>
    public string PatternHash { get; init; } = "";

    /// <summary>
    /// Total potential savings across all matching groups.
    /// </summary>
    public long TotalPotentialSavings { get; init; }
}

/// <summary>
/// Result of applying a pattern rule to multiple duplicate groups.
/// </summary>
public record ApplyPatternRuleResultDto
{
    /// <summary>
    /// Number of duplicate groups that were updated.
    /// </summary>
    public int GroupsUpdated { get; init; }

    /// <summary>
    /// Number of groups skipped (e.g., no file in preferred directory).
    /// </summary>
    public int GroupsSkipped { get; init; }

    /// <summary>
    /// Total files marked as original across all updated groups.
    /// </summary>
    public int FilesMarkedAsOriginal { get; init; }

    /// <summary>
    /// ID of the next unresolved group with a different pattern.
    /// Null if all groups are resolved.
    /// </summary>
    public Guid? NextUnresolvedGroupId { get; init; }

    /// <summary>
    /// Reasons why specific groups were skipped.
    /// </summary>
    public IReadOnlyList<string>? SkippedGroupReasons { get; init; }
}
