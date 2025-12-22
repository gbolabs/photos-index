namespace Database.Entities;

/// <summary>
/// Configures priority rules for automatic original file selection.
/// </summary>
public class SelectionPreference
{
    public Guid Id { get; set; }

    /// <summary>
    /// Path prefix to match (e.g., "/photos/").
    /// </summary>
    public required string PathPrefix { get; set; }

    /// <summary>
    /// Priority score (0-100). Higher is preferred.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Order in which rules are evaluated.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// When the preference was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the preference was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
