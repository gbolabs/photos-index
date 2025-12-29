namespace Database.Entities;

/// <summary>
/// Represents a size-based rule for hiding files.
/// Files with dimensions at or below the specified thresholds will be hidden.
/// </summary>
public class HiddenSizeRule
{
    public Guid Id { get; set; }

    /// <summary>
    /// Maximum width in pixels. Files with width at or below this will match.
    /// </summary>
    public int MaxWidth { get; set; }

    /// <summary>
    /// Maximum height in pixels. Files with height at or below this will match.
    /// </summary>
    public int MaxHeight { get; set; }

    /// <summary>
    /// Optional description explaining why files matching this size are hidden.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// When this size rule was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Navigation property for files hidden by this size rule.
    /// </summary>
    public ICollection<IndexedFile> HiddenFiles { get; set; } = [];
}
