namespace IndexingService.Models;

/// <summary>
/// Progress information during hash computation.
/// </summary>
public record HashProgress
{
    /// <summary>
    /// Number of bytes processed so far.
    /// </summary>
    public long BytesProcessed { get; init; }

    /// <summary>
    /// Total bytes to process.
    /// </summary>
    public long TotalBytes { get; init; }

    /// <summary>
    /// Percentage complete (0-100).
    /// </summary>
    public double PercentComplete => TotalBytes > 0 ? (double)BytesProcessed / TotalBytes * 100 : 0;
}
