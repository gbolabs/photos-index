namespace Database.Enums;

/// <summary>
/// Status values for duplicate group workflow.
/// See ADR-014 for complete workflow documentation.
/// </summary>
public enum DuplicateGroupStatus
{
    /// <summary>
    /// Initial state. No original file has been selected yet.
    /// Eligible for algorithm re-run.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Algorithm has suggested an original file based on configured weights/heuristics.
    /// Can be overwritten by re-running the algorithm with different parameters.
    /// Eligible for algorithm re-run.
    /// </summary>
    AutoSelected = 1,

    /// <summary>
    /// Human has explicitly chosen the original file (manual selection or pattern apply).
    /// Protected from algorithm overwrites - only explicit user action can change this.
    /// </summary>
    Validated = 2,

    /// <summary>
    /// A cleaning job is actively processing this group's non-original files.
    /// Files are being archived to MinIO and deleted from source.
    /// </summary>
    Cleaning = 3,

    /// <summary>
    /// Cleaning job failed for one or more files in this group.
    /// Requires user intervention to retry or reset.
    /// </summary>
    CleaningFailed = 4,

    /// <summary>
    /// All non-original files have been successfully archived and deleted.
    /// Terminal state under normal operation.
    /// </summary>
    Cleaned = 5
}
