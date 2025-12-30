using Database.Enums;

namespace Database.Entities;

public class DuplicateGroup
{
    public Guid Id { get; set; }

    public required string Hash { get; set; }

    public int FileCount { get; set; }

    public long TotalSize { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Current workflow status. See ADR-014 for status definitions.
    /// </summary>
    public DuplicateGroupStatus Status { get; set; } = DuplicateGroupStatus.Pending;

    /// <summary>
    /// When the group was validated (status changed to Validated).
    /// </summary>
    public DateTime? ValidatedAt { get; set; }

    /// <summary>
    /// ID of the file chosen to be kept (the original).
    /// </summary>
    public Guid? KeptFileId { get; set; }

    /// <summary>
    /// When the cleaning job started processing this group.
    /// </summary>
    public DateTime? CleaningStartedAt { get; set; }

    /// <summary>
    /// When the cleaning job completed (success or failure).
    /// </summary>
    public DateTime? CleaningCompletedAt { get; set; }

    // Keyboard review session fields
    public DateTime? LastReviewedAt { get; set; }

    public int? ReviewOrder { get; set; }

    public Guid? ReviewSessionId { get; set; }

    // Navigation properties
    public ICollection<IndexedFile> Files { get; set; } = new List<IndexedFile>();

    public SelectionSession? ReviewSession { get; set; }
}
