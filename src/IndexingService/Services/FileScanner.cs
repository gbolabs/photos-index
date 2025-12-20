using System.Runtime.CompilerServices;
using IndexingService.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IndexingService.Services;

/// <summary>
/// Scans directories for image files with configurable filtering.
/// </summary>
public class FileScanner : IFileScanner
{
    private readonly ScannerOptions _options;
    private readonly ILogger<FileScanner> _logger;
    private readonly HashSet<string> _supportedExtensions;

    public FileScanner(IOptions<ScannerOptions> options, ILogger<FileScanner> logger)
    {
        _options = options.Value;
        _logger = logger;
        _supportedExtensions = new HashSet<string>(
            _options.SupportedExtensions.Select(e => e.ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);
    }

    public async IAsyncEnumerable<ScannedFile> ScanAsync(
        string directoryPath,
        bool includeSubdirectories,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        IProgress<ScanProgress>? progress = null)
    {
        if (!Directory.Exists(directoryPath))
        {
            _logger.LogWarning("Directory does not exist: {Path}", directoryPath);
            yield break;
        }

        var filesFound = 0;
        var directoriesScanned = 0;
        var errors = 0;

        var searchOption = includeSubdirectories
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = includeSubdirectories,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            MaxRecursionDepth = _options.MaxDepth > 0 ? _options.MaxDepth : int.MaxValue,
            AttributesToSkip = _options.SkipHiddenFiles
                ? FileAttributes.Hidden | FileAttributes.System
                : FileAttributes.System
        };

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directoryPath, "*", enumerationOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate directory: {Path}", directoryPath);
            yield break;
        }

        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Skip hidden files if configured
            var fileName = Path.GetFileName(filePath);
            if (_options.SkipHiddenFiles && fileName.StartsWith('.'))
            {
                continue;
            }

            // Skip hidden directories if configured
            if (_options.SkipHiddenDirectories)
            {
                var directory = Path.GetDirectoryName(filePath);
                if (directory is not null && ContainsHiddenDirectory(directory, directoryPath))
                {
                    continue;
                }
            }

            // Check extension
            var extension = Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(extension) || !_supportedExtensions.Contains(extension))
            {
                continue;
            }

            // Get file info
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(filePath);

                // Skip symbolic links if not configured to follow
                if (!_options.FollowSymlinks && fileInfo.LinkTarget is not null)
                {
                    continue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get file info: {Path}", filePath);
                errors++;
                continue;
            }

            filesFound++;

            // Update directory count (approximate by checking if we're in a new directory)
            var currentDir = Path.GetDirectoryName(filePath) ?? directoryPath;
            directoriesScanned = CountDirectoryDepth(currentDir, directoryPath);

            progress?.Report(new ScanProgress
            {
                FilesFound = filesFound,
                DirectoriesScanned = directoriesScanned,
                Errors = errors,
                CurrentDirectory = currentDir
            });

            yield return new ScannedFile
            {
                FullPath = filePath,
                FileName = fileName,
                Extension = extension,
                FileSizeBytes = fileInfo.Length,
                LastModifiedUtc = fileInfo.LastWriteTimeUtc
            };

            // Yield control to allow cancellation processing
            await Task.Yield();
        }

        _logger.LogInformation(
            "Scan completed: {FilesFound} files found in {DirectoriesScanned} directories, {Errors} errors",
            filesFound, directoriesScanned, errors);
    }

    private bool ContainsHiddenDirectory(string fullPath, string basePath)
    {
        var relativePath = Path.GetRelativePath(basePath, fullPath);
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return parts.Any(p => p.StartsWith('.') && p != "." && p != "..");
    }

    private static int CountDirectoryDepth(string currentDir, string baseDir)
    {
        var relativePath = Path.GetRelativePath(baseDir, currentDir);
        if (relativePath == ".")
            return 1;

        return relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length + 1;
    }
}
