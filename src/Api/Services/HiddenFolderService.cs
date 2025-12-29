using Database;
using Database.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Dtos;

namespace Api.Services;

/// <summary>
/// Service implementation for managing hidden folder rules.
/// </summary>
public class HiddenFolderService : IHiddenFolderService
{
    private readonly PhotosDbContext _dbContext;
    private readonly ILogger<HiddenFolderService> _logger;

    public HiddenFolderService(PhotosDbContext dbContext, ILogger<HiddenFolderService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<HiddenFolderDto>> GetAllAsync(CancellationToken ct)
    {
        var folders = await _dbContext.HiddenFolders
            .AsNoTracking()
            .OrderBy(h => h.FolderPath)
            .Select(h => new HiddenFolderDto
            {
                Id = h.Id,
                FolderPath = h.FolderPath,
                Description = h.Description,
                CreatedAt = h.CreatedAt,
                AffectedFileCount = _dbContext.IndexedFiles
                    .Count(f => f.HiddenByFolderId == h.Id)
            })
            .ToListAsync(ct);

        return folders;
    }

    public async Task<IReadOnlyList<FolderPathDto>> GetFolderPathsAsync(string? search, CancellationToken ct)
    {
        var query = _dbContext.IndexedFiles.AsNoTracking();

        // Get distinct folder paths from file paths
        var folderPaths = await query
            .Select(f => f.FilePath.Substring(0, f.FilePath.LastIndexOf('/')))
            .Distinct()
            .ToListAsync(ct);

        // Filter by search term if provided
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            folderPaths = folderPaths
                .Where(p => p.ToLower().Contains(searchLower))
                .ToList();
        }

        // Get file counts for each folder path
        var allFiles = await _dbContext.IndexedFiles
            .AsNoTracking()
            .Select(f => f.FilePath)
            .ToListAsync(ct);

        var result = folderPaths
            .Select(path => new FolderPathDto
            {
                Path = path,
                FileCount = allFiles.Count(f => f.StartsWith(path + "/") || GetFolderPath(f) == path)
            })
            .OrderBy(p => p.Path)
            .Take(100) // Limit results for autocomplete
            .ToList();

        return result;
    }

    private static string GetFolderPath(string filePath)
    {
        var lastSlash = filePath.LastIndexOf('/');
        return lastSlash > 0 ? filePath[..lastSlash] : filePath;
    }

    public async Task<HiddenFolderDto> CreateAsync(CreateHiddenFolderRequest request, CancellationToken ct)
    {
        // Normalize folder path
        var folderPath = request.FolderPath.TrimEnd('/');

        // Check if rule already exists
        var existing = await _dbContext.HiddenFolders
            .FirstOrDefaultAsync(h => h.FolderPath == folderPath, ct);

        if (existing is not null)
        {
            _logger.LogWarning("Hidden folder rule already exists for path: {Path}", folderPath);
            throw new InvalidOperationException($"Hidden folder rule already exists for path: {folderPath}");
        }

        var hiddenFolder = new HiddenFolder
        {
            Id = Guid.NewGuid(),
            FolderPath = folderPath,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.HiddenFolders.Add(hiddenFolder);

        // Mark all matching files as hidden
        var matchingFiles = await _dbContext.IndexedFiles
            .Where(f => f.FilePath.StartsWith(folderPath + "/") || f.FilePath.StartsWith(folderPath + "\\"))
            .Where(f => !f.IsHidden) // Only hide files that aren't already hidden
            .ToListAsync(ct);

        foreach (var file in matchingFiles)
        {
            file.IsHidden = true;
            file.HiddenCategory = HiddenCategory.FolderRule;
            file.HiddenAt = DateTime.UtcNow;
            file.HiddenByFolderId = hiddenFolder.Id;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created hidden folder rule for path {Path}, affected {Count} files",
            folderPath, matchingFiles.Count);

        return new HiddenFolderDto
        {
            Id = hiddenFolder.Id,
            FolderPath = hiddenFolder.FolderPath,
            Description = hiddenFolder.Description,
            CreatedAt = hiddenFolder.CreatedAt,
            AffectedFileCount = matchingFiles.Count
        };
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var hiddenFolder = await _dbContext.HiddenFolders.FindAsync([id], ct);

        if (hiddenFolder is null)
            return false;

        // Unhide files that were hidden by this folder rule
        var affectedFiles = await _dbContext.IndexedFiles
            .Where(f => f.HiddenByFolderId == id)
            .ToListAsync(ct);

        foreach (var file in affectedFiles)
        {
            file.IsHidden = false;
            file.HiddenCategory = null;
            file.HiddenAt = null;
            file.HiddenByFolderId = null;
        }

        _dbContext.HiddenFolders.Remove(hiddenFolder);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Deleted hidden folder rule {Id} for path {Path}, unhid {Count} files",
            id, hiddenFolder.FolderPath, affectedFiles.Count);

        return true;
    }

    public async Task<int> HideFilesAsync(HideFilesRequest request, CancellationToken ct)
    {
        var files = await _dbContext.IndexedFiles
            .Where(f => request.FileIds.Contains(f.Id))
            .Where(f => !f.IsHidden) // Only hide files that aren't already hidden
            .ToListAsync(ct);

        foreach (var file in files)
        {
            file.IsHidden = true;
            file.HiddenCategory = HiddenCategory.Manual;
            file.HiddenAt = DateTime.UtcNow;
            file.HiddenByFolderId = null; // Manual hide, not by folder rule
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Manually hid {Count} files", files.Count);

        return files.Count;
    }

    public async Task<int> UnhideFilesAsync(HideFilesRequest request, CancellationToken ct)
    {
        var files = await _dbContext.IndexedFiles
            .Where(f => request.FileIds.Contains(f.Id))
            .Where(f => f.IsHidden)
            .ToListAsync(ct);

        foreach (var file in files)
        {
            file.IsHidden = false;
            file.HiddenCategory = null;
            file.HiddenAt = null;
            file.HiddenByFolderId = null;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Unhid {Count} files", files.Count);

        return files.Count;
    }
}
