namespace Shared.Requests;

/// <summary>
/// Request to validate a batch of duplicate groups.
/// </summary>
public record ValidateBatchRequest
{
    /// <summary>
    /// Number of groups to validate in this batch.
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// Optional status filter to apply before selecting groups to validate.
    /// </summary>
    public string? StatusFilter { get; init; }
}
