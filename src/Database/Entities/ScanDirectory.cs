namespace Database.Entities;

public class ScanDirectory
{
    public Guid Id { get; set; }

    public required string Path { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime? LastScannedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public int FileCount { get; set; } = 0;
}
