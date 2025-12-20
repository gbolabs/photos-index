namespace IndexingService.Models;

/// <summary>
/// Progress information during directory scanning.
/// </summary>
public record ScanProgress
{
    /// <summary>
    /// Number of matching files found so far.
    /// </summary>
    public int FilesFound { get; init; }

    /// <summary>
    /// Number of directories scanned so far.
    /// </summary>
    public int DirectoriesScanned { get; init; }

    /// <summary>
    /// Number of errors encountered.
    /// </summary>
    public int Errors { get; init; }

    /// <summary>
    /// Current directory being scanned.
    /// </summary>
    public string CurrentDirectory { get; init; } = "";
}
