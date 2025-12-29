namespace Database.Entities;

/// <summary>
/// Represents a folder path rule for hiding files.
/// Files within this folder path will be automatically hidden.
/// </summary>
public class HiddenFolder
{
    public Guid Id { get; set; }

    /// <summary>
    /// The folder path pattern. Files with paths starting with this value will be hidden.
    /// </summary>
    public required string FolderPath { get; set; }

    /// <summary>
    /// Optional description explaining why files in this folder are hidden.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// When this hidden folder rule was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Navigation property for files hidden by this folder rule.
    /// </summary>
    public ICollection<IndexedFile> HiddenFiles { get; set; } = [];
}
