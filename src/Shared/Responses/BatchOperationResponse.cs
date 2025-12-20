namespace Shared.Responses;

/// <summary>
/// Response for batch operations.
/// </summary>
public record BatchOperationResponse
{
    public int TotalRequested { get; init; }
    public int Succeeded { get; init; }
    public int Failed { get; init; }
    public IReadOnlyList<BatchOperationError>? Errors { get; init; }

    public bool HasErrors => Failed > 0;
}

public record BatchOperationError
{
    public required string Item { get; init; }
    public required string Error { get; init; }
}
