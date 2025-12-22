namespace Shared.Dtos;

public record FileNeedsReindexDto
{
    public required string FilePath { get; init; }
    public DateTime LastModifiedAt { get; init; }
    public bool NeedsReindex { get; init; }
}
