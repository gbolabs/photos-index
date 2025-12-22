namespace Shared.Requests;

/// <summary>
/// Request to validate duplicate groups.
/// </summary>
public record ValidateDuplicatesRequest
{
    /// <summary>
    /// IDs of duplicate groups to validate.
    /// </summary>
    public required List<Guid> GroupIds { get; init; }

    /// <summary>
    /// Optional ID of the file to keep (mark as kept).
    /// </summary>
    public Guid? KeptFileId { get; init; }
}
