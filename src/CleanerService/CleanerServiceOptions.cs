namespace CleanerService;

/// <summary>
/// Configuration options for the CleanerService.
/// </summary>
public class CleanerServiceOptions
{
    public const string ConfigSection = "CleanerService";

    /// <summary>
    /// Base URL of the API (e.g., https://tn.isago.ch:8053)
    /// </summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5000";

    /// <summary>
    /// Whether dry-run mode is enabled (default: true for safety)
    /// When true, files are archived but NOT deleted from disk.
    /// </summary>
    public bool DryRunEnabled { get; set; } = true;

    /// <summary>
    /// Maximum concurrent file operations
    /// </summary>
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>
    /// Timeout for file uploads (in seconds)
    /// </summary>
    public int UploadTimeoutSeconds { get; set; } = 300;
}
