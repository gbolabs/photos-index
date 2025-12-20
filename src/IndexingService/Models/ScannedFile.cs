namespace IndexingService.Models;

/// <summary>
/// Represents a file discovered during scanning.
/// </summary>
public record ScannedFile
{
    /// <summary>
    /// Full absolute path to the file.
    /// </summary>
    public required string FullPath { get; init; }

    /// <summary>
    /// File name without directory.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// File extension including the dot (e.g., ".jpg").
    /// </summary>
    public required string Extension { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// Last modification time in UTC.
    /// </summary>
    public DateTime LastModifiedUtc { get; init; }
}
