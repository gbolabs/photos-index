namespace Database.Entities;

public class DuplicateGroup
{
    public Guid Id { get; set; }

    public required string Hash { get; set; }

    public int FileCount { get; set; }

    public long TotalSize { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    // Validation fields for batch processing
    public string Status { get; set; } = "pending"; // pending, auto-selected, conflict, validated, cleaned

    public DateTime? ValidatedAt { get; set; }

    public Guid? KeptFileId { get; set; }

    // Navigation property
    public ICollection<IndexedFile> Files { get; set; } = new List<IndexedFile>();
}
