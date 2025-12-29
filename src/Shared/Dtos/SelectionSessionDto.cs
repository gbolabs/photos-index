namespace Shared.Dtos;

/// <summary>
/// DTO for a keyboard-driven duplicate review session.
/// </summary>
public record SelectionSessionDto
{
    public Guid Id { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? ResumedAt { get; init; }

    public DateTime? CompletedAt { get; init; }

    public required string Status { get; init; }

    public int TotalGroups { get; init; }

    public int GroupsProposed { get; init; }

    public int GroupsValidated { get; init; }

    public int GroupsSkipped { get; init; }

    public Guid? CurrentGroupId { get; init; }

    public Guid? LastReviewedGroupId { get; init; }

    public DateTime? LastActivityAt { get; init; }
}

/// <summary>
/// Progress information for the current session.
/// </summary>
public record SessionProgressDto
{
    public int Proposed { get; init; }

    public int Validated { get; init; }

    public int Skipped { get; init; }

    public int Remaining { get; init; }

    public Guid? NextGroupId { get; init; }

    public double ProgressPercent { get; init; }
}

/// <summary>
/// Result of a propose/validate/skip action.
/// </summary>
public record ReviewActionResultDto
{
    public bool Success { get; init; }

    public Guid? NextGroupId { get; init; }

    public string? Message { get; init; }
}

/// <summary>
/// Request to start a new review session.
/// </summary>
public record StartSessionRequest
{
    public bool ResumeExisting { get; init; } = true;
}

/// <summary>
/// Request to propose a file as original for a group.
/// </summary>
public record ProposeOriginalRequest
{
    public Guid FileId { get; init; }
}
