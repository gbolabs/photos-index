namespace Shared.Requests;

/// <summary>
/// Request to recalculate original file selections.
/// </summary>
public record RecalculateOriginalsRequest
{
    /// <summary>
    /// Scope of recalculation: "pending" (only unresolved groups) or "all" (re-evaluate all groups).
    /// </summary>
    public string Scope { get; init; } = "pending";

    /// <summary>
    /// If true, return preview of changes without applying them.
    /// </summary>
    public bool Preview { get; init; } = false;
}
