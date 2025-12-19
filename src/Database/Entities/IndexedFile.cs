namespace Database.Entities;

public class IndexedFile
{
    public Guid Id { get; set; }

    public required string FilePath { get; set; }

    public required string FileName { get; set; }

    public long FileSize { get; set; }

    public required string FileHash { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public DateTime IndexedAt { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public string? ThumbnailPath { get; set; }

    public bool IsDuplicate { get; set; }

    public Guid? DuplicateGroupId { get; set; }

    // Navigation property
    public DuplicateGroup? DuplicateGroup { get; set; }
}
