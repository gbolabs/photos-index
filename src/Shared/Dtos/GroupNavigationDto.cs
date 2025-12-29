namespace Shared.Dtos;

/// <summary>
/// Navigation information for moving between duplicate groups.
/// </summary>
public record GroupNavigationDto
{
    /// <summary>
    /// ID of the previous group in the list, or null if this is the first group.
    /// </summary>
    public Guid? PreviousGroupId { get; init; }

    /// <summary>
    /// ID of the next group in the list, or null if this is the last group.
    /// </summary>
    public Guid? NextGroupId { get; init; }

    /// <summary>
    /// Current position (1-based) in the list.
    /// </summary>
    public int CurrentPosition { get; init; }

    /// <summary>
    /// Total number of groups in the filtered list.
    /// </summary>
    public int TotalGroups { get; init; }
}
