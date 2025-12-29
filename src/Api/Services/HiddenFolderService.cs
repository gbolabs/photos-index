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

    public async Task<int> GetHiddenCountAsync(CancellationToken ct)
    {
        return await _dbContext.IndexedFiles
            .AsNoTracking()
            .CountAsync(f => f.IsHidden, ct);
    }

    // Size rule methods

    public async Task<IReadOnlyList<HiddenSizeRuleDto>> GetAllSizeRulesAsync(CancellationToken ct)
    {
        var rules = await _dbContext.HiddenSizeRules
            .AsNoTracking()
            .OrderBy(r => r.MaxWidth)
            .ThenBy(r => r.MaxHeight)
            .Select(r => new HiddenSizeRuleDto
            {
                Id = r.Id,
                MaxWidth = r.MaxWidth,
                MaxHeight = r.MaxHeight,
                Description = r.Description,
                CreatedAt = r.CreatedAt,
                AffectedFileCount = _dbContext.IndexedFiles
                    .Count(f => f.HiddenBySizeRuleId == r.Id)
            })
            .ToListAsync(ct);

        return rules;
    }

    public async Task<SizeRulePreviewDto> PreviewSizeRuleAsync(int maxWidth, int maxHeight, CancellationToken ct)
    {
        var matchingFiles = await _dbContext.IndexedFiles
            .AsNoTracking()
            .Where(f => !f.IsHidden) // Only show files that aren't already hidden
            .Where(f => f.Width.HasValue && f.Height.HasValue)
            .Where(f => f.Width <= maxWidth && f.Height <= maxHeight)
            .Select(f => new { f.Width, f.Height, f.FileSize })
            .ToListAsync(ct);

        var sizeGroups = matchingFiles
            .GroupBy(f => new { f.Width, f.Height })
            .Select(g => new SizeGroupDto
            {
                Width = g.Key.Width!.Value,
                Height = g.Key.Height!.Value,
                FileCount = g.Count(),
                TotalSizeBytes = g.Sum(f => f.FileSize)
            })
            .OrderBy(g => g.Width)
            .ThenBy(g => g.Height)
            .ToList();

        return new SizeRulePreviewDto
        {
            TotalFiles = matchingFiles.Count,
            TotalSizeBytes = matchingFiles.Sum(f => f.FileSize),
            SizeGroups = sizeGroups
        };
    }

    public async Task<HiddenSizeRuleDto> CreateSizeRuleAsync(CreateHiddenSizeRuleRequest request, CancellationToken ct)
    {
        // Check if rule with same dimensions already exists
        var existing = await _dbContext.HiddenSizeRules
            .FirstOrDefaultAsync(r => r.MaxWidth == request.MaxWidth && r.MaxHeight == request.MaxHeight, ct);

        if (existing is not null)
        {
            _logger.LogWarning("Size rule already exists for {Width}x{Height}", request.MaxWidth, request.MaxHeight);
            throw new InvalidOperationException($"Size rule already exists for {request.MaxWidth}x{request.MaxHeight}");
        }

        var sizeRule = new HiddenSizeRule
        {
            Id = Guid.NewGuid(),
            MaxWidth = request.MaxWidth,
            MaxHeight = request.MaxHeight,
            Description = request.Description ?? $"Images {request.MaxWidth}x{request.MaxHeight} or smaller",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.HiddenSizeRules.Add(sizeRule);

        // Mark all matching files as hidden
        var matchingFiles = await _dbContext.IndexedFiles
            .Where(f => !f.IsHidden)
            .Where(f => f.Width.HasValue && f.Height.HasValue)
            .Where(f => f.Width <= request.MaxWidth && f.Height <= request.MaxHeight)
            .ToListAsync(ct);

        foreach (var file in matchingFiles)
        {
            file.IsHidden = true;
            file.HiddenCategory = HiddenCategory.SizeRule;
            file.HiddenAt = DateTime.UtcNow;
            file.HiddenBySizeRuleId = sizeRule.Id;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created size rule for {Width}x{Height}, affected {Count} files",
            request.MaxWidth, request.MaxHeight, matchingFiles.Count);

        return new HiddenSizeRuleDto
        {
            Id = sizeRule.Id,
            MaxWidth = sizeRule.MaxWidth,
            MaxHeight = sizeRule.MaxHeight,
            Description = sizeRule.Description,
            CreatedAt = sizeRule.CreatedAt,
            AffectedFileCount = matchingFiles.Count
        };
    }

    public async Task<bool> DeleteSizeRuleAsync(Guid id, CancellationToken ct)
    {
        var sizeRule = await _dbContext.HiddenSizeRules.FindAsync([id], ct);

        if (sizeRule is null)
            return false;

        // Unhide files that were hidden by this size rule
        var affectedFiles = await _dbContext.IndexedFiles
            .Where(f => f.HiddenBySizeRuleId == id)
            .ToListAsync(ct);

        foreach (var file in affectedFiles)
        {
            file.IsHidden = false;
            file.HiddenCategory = null;
            file.HiddenAt = null;
            file.HiddenBySizeRuleId = null;
        }

        _dbContext.HiddenSizeRules.Remove(sizeRule);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Deleted size rule {Id} for {Width}x{Height}, unhid {Count} files",
            id, sizeRule.MaxWidth, sizeRule.MaxHeight, affectedFiles.Count);

        return true;
    }
}
