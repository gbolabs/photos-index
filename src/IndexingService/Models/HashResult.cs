namespace IndexingService.Models;

/// <summary>
/// Result of computing a file hash.
/// </summary>
public record HashResult
{
    /// <summary>
    /// Full path to the file that was hashed.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// SHA256 hash as lowercase hex string.
    /// </summary>
    public required string Hash { get; init; }

    /// <summary>
    /// Number of bytes processed.
    /// </summary>
    public long BytesProcessed { get; init; }

    /// <summary>
    /// Time taken to compute the hash.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Whether the hash was computed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if the hash computation failed.
    /// </summary>
    public string? Error { get; init; }

    public static HashResult Failed(string filePath, string error) => new()
    {
        FilePath = filePath,
        Hash = string.Empty,
        Success = false,
        Error = error
    };
}
