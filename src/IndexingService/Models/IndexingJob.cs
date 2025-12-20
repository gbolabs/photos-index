namespace IndexingService.Models;

/// <summary>
/// Represents an indexing job for a specific directory.
/// </summary>
public record IndexingJob
{
    /// <summary>
    /// Unique identifier for the scan directory.
    /// </summary>
    public required Guid DirectoryId { get; init; }

    /// <summary>
    /// Path to the directory to scan.
    /// </summary>
    public required string DirectoryPath { get; init; }

    /// <summary>
    /// When the job started.
    /// </summary>
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When the job completed (null if still running).
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Total number of files scanned.
    /// </summary>
    public int FilesScanned { get; init; }

    /// <summary>
    /// Number of files processed (hashed + metadata extracted).
    /// </summary>
    public int FilesProcessed { get; init; }

    /// <summary>
    /// Number of files successfully ingested to API.
    /// </summary>
    public int FilesIngested { get; init; }

    /// <summary>
    /// Number of files that failed processing.
    /// </summary>
    public int FilesFailed { get; init; }

    /// <summary>
    /// Whether the job completed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if the job failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Duration of the job.
    /// </summary>
    public TimeSpan Duration => (CompletedAt ?? DateTime.UtcNow) - StartedAt;

    /// <summary>
    /// Creates a new job that is starting.
    /// </summary>
    public static IndexingJob Start(Guid directoryId, string directoryPath) => new()
    {
        DirectoryId = directoryId,
        DirectoryPath = directoryPath,
        StartedAt = DateTime.UtcNow
    };

    /// <summary>
    /// Creates a completed job.
    /// </summary>
    public IndexingJob Complete(int filesScanned, int filesProcessed, int filesIngested, int filesFailed) => this with
    {
        CompletedAt = DateTime.UtcNow,
        FilesScanned = filesScanned,
        FilesProcessed = filesProcessed,
        FilesIngested = filesIngested,
        FilesFailed = filesFailed,
        Success = true
    };

    /// <summary>
    /// Creates a failed job.
    /// </summary>
    public IndexingJob Fail(string error) => this with
    {
        CompletedAt = DateTime.UtcNow,
        Success = false,
        Error = error
    };
}
