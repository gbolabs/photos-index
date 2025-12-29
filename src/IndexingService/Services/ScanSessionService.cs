using System.Collections.Concurrent;

namespace IndexingService.Services;

/// <summary>
/// Tracks the current scanning session to avoid rescanning directories
/// and files that have already been processed in this session.
/// </summary>
public interface IScanSessionService
{
    /// <summary>
    /// Gets the current session ID.
    /// </summary>
    Guid SessionId { get; }

    /// <summary>
    /// Starts a new scanning session, clearing all previous tracking data.
    /// </summary>
    void StartNewSession();

    /// <summary>
    /// Checks if a directory has been fully scanned in the current session.
    /// </summary>
    bool IsDirectoryScanned(string directoryPath);

    /// <summary>
    /// Marks a directory as fully scanned in the current session.
    /// </summary>
    void MarkDirectoryScanned(string directoryPath);

    /// <summary>
    /// Marks a file as processed in the current session.
    /// </summary>
    void MarkFileProcessed(string filePath);

    /// <summary>
    /// Checks if a file has been processed in the current session.
    /// </summary>
    bool IsFileProcessed(string filePath);

    /// <summary>
    /// Gets the count of files processed in the current session.
    /// </summary>
    int ProcessedFileCount { get; }

    /// <summary>
    /// Gets the count of directories scanned in the current session.
    /// </summary>
    int ScannedDirectoryCount { get; }

    /// <summary>
    /// Checks if a directory or any of its parent directories have been scanned.
    /// Uses hierarchical masking to skip subdirectories of already-scanned paths.
    /// </summary>
    bool IsPathCoveredByScannedDirectory(string path);

    /// <summary>
    /// Gets the session start time.
    /// </summary>
    DateTime SessionStartTime { get; }
}

/// <summary>
/// Implementation of scan session tracking.
/// Thread-safe for concurrent access during scanning.
/// </summary>
public class ScanSessionService : IScanSessionService
{
    private readonly ConcurrentDictionary<string, DateTime> _scannedDirectories = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _processedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sessionLock = new();

    public Guid SessionId { get; private set; } = Guid.NewGuid();
    public DateTime SessionStartTime { get; private set; } = DateTime.UtcNow;

    public int ProcessedFileCount => _processedFiles.Count;
    public int ScannedDirectoryCount => _scannedDirectories.Count;

    public void StartNewSession()
    {
        lock (_sessionLock)
        {
            SessionId = Guid.NewGuid();
            SessionStartTime = DateTime.UtcNow;
            _scannedDirectories.Clear();
            _processedFiles.Clear();
        }
    }

    public bool IsDirectoryScanned(string directoryPath)
    {
        var normalizedPath = NormalizePath(directoryPath);
        return _scannedDirectories.ContainsKey(normalizedPath);
    }

    public void MarkDirectoryScanned(string directoryPath)
    {
        var normalizedPath = NormalizePath(directoryPath);
        _scannedDirectories.TryAdd(normalizedPath, DateTime.UtcNow);
    }

    public void MarkFileProcessed(string filePath)
    {
        var normalizedPath = NormalizePath(filePath);
        _processedFiles.TryAdd(normalizedPath, true);
    }

    public bool IsFileProcessed(string filePath)
    {
        var normalizedPath = NormalizePath(filePath);
        return _processedFiles.ContainsKey(normalizedPath);
    }

    public bool IsPathCoveredByScannedDirectory(string path)
    {
        var normalizedPath = NormalizePath(path);

        // Check if the exact path is scanned
        if (_scannedDirectories.ContainsKey(normalizedPath))
            return true;

        // Check if any parent directory is scanned (hierarchical masking)
        foreach (var scannedDir in _scannedDirectories.Keys)
        {
            // Check if the path is under the scanned directory
            if (IsSubPath(normalizedPath, scannedDir))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if childPath is under parentPath in the directory hierarchy.
    /// </summary>
    private static bool IsSubPath(string childPath, string parentPath)
    {
        // Ensure paths end with separator for proper comparison
        var parentWithSep = parentPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            + Path.DirectorySeparatorChar;

        return childPath.StartsWith(parentWithSep, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        // Normalize to consistent format
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
