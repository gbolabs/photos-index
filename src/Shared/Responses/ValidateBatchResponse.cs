namespace Shared.Responses;

/// <summary>
/// Response for batch validation operation.
/// </summary>
public record ValidateBatchResponse
{
    /// <summary>
    /// Number of groups validated in this batch.
    /// </summary>
    public required int Validated { get; init; }

    /// <summary>
    /// Number of groups remaining after this batch.
    /// </summary>
    public required int Remaining { get; init; }
}
