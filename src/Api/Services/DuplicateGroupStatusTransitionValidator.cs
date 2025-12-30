using Database.Enums;

namespace Api.Services;

/// <summary>
/// Validates status transitions for DuplicateGroup entities.
/// Enforces the workflow rules defined in ADR-014.
/// </summary>
public class DuplicateGroupStatusTransitionValidator : IDuplicateGroupStatusTransitionValidator
{
    /// <summary>
    /// Set of all valid (From, To) status transitions.
    /// Any transition not in this set is invalid.
    /// </summary>
    private static readonly HashSet<(DuplicateGroupStatus From, DuplicateGroupStatus To)> ValidTransitions =
    [
        // From Pending
        (DuplicateGroupStatus.Pending, DuplicateGroupStatus.AutoSelected),  // Algorithm selection
        (DuplicateGroupStatus.Pending, DuplicateGroupStatus.Validated),     // Manual/Pattern selection

        // From AutoSelected
        (DuplicateGroupStatus.AutoSelected, DuplicateGroupStatus.AutoSelected), // Re-run algorithm
        (DuplicateGroupStatus.AutoSelected, DuplicateGroupStatus.Validated),    // User confirms
        (DuplicateGroupStatus.AutoSelected, DuplicateGroupStatus.Pending),      // User resets

        // From Validated
        (DuplicateGroupStatus.Validated, DuplicateGroupStatus.Cleaning),    // Start clean job
        (DuplicateGroupStatus.Validated, DuplicateGroupStatus.Pending),     // User resets (undo)

        // From Cleaning
        (DuplicateGroupStatus.Cleaning, DuplicateGroupStatus.Cleaned),        // Success
        (DuplicateGroupStatus.Cleaning, DuplicateGroupStatus.CleaningFailed), // Failure

        // From CleaningFailed
        (DuplicateGroupStatus.CleaningFailed, DuplicateGroupStatus.Validated), // Retry
        (DuplicateGroupStatus.CleaningFailed, DuplicateGroupStatus.Pending),   // Reset

        // From Cleaned (admin only)
        (DuplicateGroupStatus.Cleaned, DuplicateGroupStatus.Pending), // Admin reset
    ];

    /// <summary>
    /// Statuses that can be processed by the auto-selection algorithm.
    /// Validated, Cleaning, CleaningFailed, and Cleaned are protected.
    /// </summary>
    public static readonly HashSet<DuplicateGroupStatus> AlgorithmEligibleStatuses =
    [
        DuplicateGroupStatus.Pending,
        DuplicateGroupStatus.AutoSelected
    ];

    /// <inheritdoc />
    public bool CanTransition(DuplicateGroupStatus from, DuplicateGroupStatus to)
        => ValidTransitions.Contains((from, to));

    /// <inheritdoc />
    public void ValidateTransition(DuplicateGroupStatus from, DuplicateGroupStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException(
                $"Invalid status transition from '{from}' to '{to}'. " +
                $"See ADR-014 for valid transitions.");
        }
    }

    /// <inheritdoc />
    public bool IsEligibleForAlgorithm(DuplicateGroupStatus status)
        => AlgorithmEligibleStatuses.Contains(status);

    /// <inheritdoc />
    public IReadOnlySet<DuplicateGroupStatus> GetValidTargetStatuses(DuplicateGroupStatus from)
    {
        return ValidTransitions
            .Where(t => t.From == from)
            .Select(t => t.To)
            .ToHashSet();
    }
}

/// <summary>
/// Interface for DuplicateGroup status transition validation.
/// </summary>
public interface IDuplicateGroupStatusTransitionValidator
{
    /// <summary>
    /// Checks if a transition from one status to another is valid.
    /// </summary>
    bool CanTransition(DuplicateGroupStatus from, DuplicateGroupStatus to);

    /// <summary>
    /// Validates a transition and throws if invalid.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when transition is not valid.</exception>
    void ValidateTransition(DuplicateGroupStatus from, DuplicateGroupStatus to);

    /// <summary>
    /// Checks if a group with the given status can be processed by the auto-selection algorithm.
    /// </summary>
    bool IsEligibleForAlgorithm(DuplicateGroupStatus status);

    /// <summary>
    /// Gets all valid target statuses from a given status.
    /// </summary>
    IReadOnlySet<DuplicateGroupStatus> GetValidTargetStatuses(DuplicateGroupStatus from);
}
