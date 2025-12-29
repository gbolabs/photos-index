namespace Database.Entities;

/// <summary>
/// Tracks a keyboard-driven duplicate review session.
/// Persisted to DB to allow resumption after restarts.
/// </summary>
public class SelectionSession
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ResumedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Session status: active, paused, completed
    /// </summary>
    public string Status { get; set; } = "active";

    // Progress tracking
    public int TotalGroups { get; set; }

    public int GroupsProposed { get; set; }

    public int GroupsValidated { get; set; }

    public int GroupsSkipped { get; set; }

    // Resumption point
    public Guid? CurrentGroupId { get; set; }

    public Guid? LastReviewedGroupId { get; set; }

    public DateTime? LastActivityAt { get; set; }

    // Navigation properties
    public ICollection<DuplicateGroup> ReviewedGroups { get; set; } = new List<DuplicateGroup>();
}
