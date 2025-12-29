using Shared.Dtos;

namespace Database.Entities;

/// <summary>
/// Represents a batch file deletion job.
/// </summary>
public class CleanerJob
{
    public Guid Id { get; set; }
    public CleanerJobStatus Status { get; set; } = CleanerJobStatus.Pending;
    public DeleteCategory Category { get; set; }
    public bool DryRun { get; set; } = true;
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public int SucceededFiles { get; set; }
    public int FailedFiles { get; set; }
    public int SkippedFiles { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public virtual ICollection<CleanerJobFile> Files { get; set; } = new List<CleanerJobFile>();
}

/// <summary>
/// Represents an individual file within a cleaner job.
/// </summary>
public class CleanerJobFile
{
    public Guid Id { get; set; }
    public Guid CleanerJobId { get; set; }
    public Guid FileId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public CleanerFileStatus Status { get; set; } = CleanerFileStatus.Pending;
    public string? ArchivePath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? ProcessedAt { get; set; }

    public virtual CleanerJob CleanerJob { get; set; } = null!;
    public virtual IndexedFile? IndexedFile { get; set; }
}
