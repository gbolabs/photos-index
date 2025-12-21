namespace Shared.Requests;

public record CheckFilesNeedReindexRequest
{
    public Guid DirectoryId { get; init; }
    public required IReadOnlyList<FileModificationInfo> Files { get; init; }
}

public record FileModificationInfo
{
    public required string FilePath { get; init; }
    public DateTime ModifiedAt { get; init; }
}
