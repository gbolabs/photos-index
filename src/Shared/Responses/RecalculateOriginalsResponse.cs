using Shared.Dtos;

namespace Shared.Responses;

/// <summary>
/// Response from recalculating original file selections.
/// </summary>
public record RecalculateOriginalsResponse
{
    /// <summary>
    /// Number of groups that were updated.
    /// </summary>
    public int Updated { get; init; }

    /// <summary>
    /// Number of groups marked as conflict (algorithm couldn't decide).
    /// </summary>
    public int Conflicts { get; init; }

    /// <summary>
    /// Preview of changes (only if Preview=true in request).
    /// </summary>
    public IReadOnlyList<DuplicateGroupDto>? Preview { get; init; }
}
