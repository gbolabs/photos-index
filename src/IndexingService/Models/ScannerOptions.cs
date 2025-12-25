namespace IndexingService.Models;

/// <summary>
/// Configuration options for the file scanner.
/// </summary>
public class ScannerOptions
{
    public const string SectionName = "Scanner";

    /// <summary>
    /// Supported image file extensions (case-insensitive).
    /// </summary>
    public string[] SupportedExtensions { get; set; } =
        [".jpg", ".jpeg", ".png", ".gif", ".heic", ".webp", ".bmp", ".tiff"];

    /// <summary>
    /// Skip files starting with a dot.
    /// </summary>
    public bool SkipHiddenFiles { get; set; } = true;

    /// <summary>
    /// Skip directories starting with a dot.
    /// </summary>
    public bool SkipHiddenDirectories { get; set; } = true;

    /// <summary>
    /// Directory names to exclude from scanning (case-insensitive).
    /// Default includes Synology's @eaDir metadata folder.
    /// </summary>
    public string[] ExcludedDirectoryNames { get; set; } = ["@eaDir", "@SynoResource", "#recycle", "@tmp"];

    /// <summary>
    /// Follow symbolic links when scanning.
    /// </summary>
    public bool FollowSymlinks { get; set; } = false;

    /// <summary>
    /// Maximum directory depth to scan (0 = unlimited).
    /// </summary>
    public int MaxDepth { get; set; } = 50;
}
