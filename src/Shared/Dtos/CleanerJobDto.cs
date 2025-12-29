using System.Text.Json.Serialization;

namespace Shared.Dtos;

/// <summary>
/// DTO for cleaner job information.
/// </summary>
public record CleanerJobDto
{
    public Guid Id { get; init; }
    public CleanerJobStatus Status { get; init; }
    public DeleteCategory Category { get; init; }
    public bool DryRun { get; init; }
    public int TotalFiles { get; init; }
    public int ProcessedFiles { get; init; }
    public int SucceededFiles { get; init; }
    public int FailedFiles { get; init; }
    public int SkippedFiles { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<CleanerJobFileDto>? Files { get; init; }
}

/// <summary>
/// DTO for individual files in a cleaner job.
/// </summary>
public record CleanerJobFileDto
{
    public Guid Id { get; init; }
    public Guid FileId { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public string FileHash { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public CleanerFileStatus Status { get; init; }
    public string? ArchivePath { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime? ProcessedAt { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CleanerJobStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CleanerFileStatus
{
    Pending,
    Uploading,
    Uploaded,
    Deleting,
    Deleted,
    Failed,
    Skipped
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeleteCategory
{
    HashDuplicate,
    NearDuplicate,
    Manual
}

/// <summary>
/// Request to delete files.
/// </summary>
public record DeleteFilesRequest
{
    public required IReadOnlyList<Guid> FileIds { get; init; }
    public DeleteCategory Category { get; init; } = DeleteCategory.HashDuplicate;
    public bool DryRun { get; init; } = true;
}

/// <summary>
/// Request to delete a single file (sent to CleanerService via SignalR).
/// </summary>
public record DeleteFileRequest
{
    public required Guid JobId { get; init; }
    public required Guid FileId { get; init; }
    public required string FilePath { get; init; }
    public required string FileHash { get; init; }
    public required long FileSize { get; init; }
    public required DeleteCategory Category { get; init; }
}

/// <summary>
/// Result of a delete operation.
/// </summary>
public record DeleteFileResult
{
    public required Guid JobId { get; init; }
    public required Guid FileId { get; init; }
    public required bool Success { get; init; }
    public string? ArchivePath { get; init; }
    public string? Error { get; init; }
    public bool WasDryRun { get; init; }
}

/// <summary>
/// Result of creating a cleaner job.
/// </summary>
public record CreateCleanerJobResult
{
    public required Guid JobId { get; init; }
    public required int FileCount { get; init; }
    public required bool DryRun { get; init; }
}
