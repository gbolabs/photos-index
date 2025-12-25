using Shared.Dtos;

namespace Api.Services;

public interface IFileIngestService
{
    Task<FileIngestResult> IngestFileAsync(FileIngestRequest request, CancellationToken ct);
    Task<IReadOnlyList<FileIngestResult>> IngestFilesAsync(IReadOnlyList<FileIngestRequest> requests, CancellationToken ct);
}

public record FileIngestRequest
{
    public Guid ScanDirectoryId { get; init; }
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required string FileHash { get; init; }
    public long FileSize { get; init; }
    public DateTime ModifiedAt { get; init; }
    public Stream? FileContent { get; init; }
    public string? ContentType { get; init; }
}

public record FileIngestResult
{
    public Guid IndexedFileId { get; init; }
    public required string FilePath { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsNewFile { get; init; }
}
