namespace Shared.Requests;

/// <summary>
/// Request to undo validation for duplicate groups.
/// </summary>
public record UndoValidationRequest
{
    /// <summary>
    /// IDs of duplicate groups to undo validation for.
    /// </summary>
    public required List<Guid> GroupIds { get; init; }
}
