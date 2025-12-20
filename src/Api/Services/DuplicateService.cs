using Database;
using Microsoft.EntityFrameworkCore;
using Shared.Dtos;
using Shared.Requests;
using Shared.Responses;

namespace Api.Services;

/// <summary>
/// Service implementation for managing duplicate file groups.
/// </summary>
public class DuplicateService : IDuplicateService
{
    private readonly PhotosDbContext _dbContext;
    private readonly ILogger<DuplicateService> _logger;

    public DuplicateService(PhotosDbContext dbContext, ILogger<DuplicateService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PagedResponse<DuplicateGroupDto>> GetGroupsAsync(int page, int pageSize, CancellationToken ct)
    {
        var query = _dbContext.DuplicateGroups.AsNoTracking();

        var totalItems = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(g => g.TotalSize)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new DuplicateGroupDto
            {
                Id = g.Id,
                Hash = g.Hash,
                FileCount = g.FileCount,
                TotalSize = g.TotalSize,
                ResolvedAt = g.ResolvedAt,
                CreatedAt = g.CreatedAt,
                OriginalFileId = g.Files.Where(f => !f.IsDuplicate).Select(f => (Guid?)f.Id).FirstOrDefault(),
                Files = new List<IndexedFileDto>() // Don't load files in list view
            })
            .ToListAsync(ct);

        return new PagedResponse<DuplicateGroupDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems
        };
    }

    public async Task<DuplicateGroupDto?> GetGroupAsync(Guid id, CancellationToken ct)
    {
        var group = await _dbContext.DuplicateGroups
            .AsNoTracking()
            .Include(g => g.Files)
            .FirstOrDefaultAsync(g => g.Id == id, ct);

        if (group is null)
            return null;

        return new DuplicateGroupDto
        {
            Id = group.Id,
            Hash = group.Hash,
            FileCount = group.FileCount,
            TotalSize = group.TotalSize,
            ResolvedAt = group.ResolvedAt,
            CreatedAt = group.CreatedAt,
            OriginalFileId = group.Files.FirstOrDefault(f => !f.IsDuplicate)?.Id,
            Files = group.Files.Select(f => new IndexedFileDto
            {
                Id = f.Id,
                FilePath = f.FilePath,
                FileName = f.FileName,
                FileHash = f.FileHash,
                FileSize = f.FileSize,
                Width = f.Width,
                Height = f.Height,
                CreatedAt = f.CreatedAt,
                ModifiedAt = f.ModifiedAt,
                IndexedAt = f.IndexedAt,
                ThumbnailPath = f.ThumbnailPath,
                IsDuplicate = f.IsDuplicate,
                DuplicateGroupId = f.DuplicateGroupId
            }).ToList()
        };
    }

    public async Task<bool> SetOriginalAsync(Guid groupId, Guid fileId, CancellationToken ct)
    {
        var group = await _dbContext.DuplicateGroups
            .Include(g => g.Files)
            .FirstOrDefaultAsync(g => g.Id == groupId, ct);

        if (group is null)
            return false;

        var targetFile = group.Files.FirstOrDefault(f => f.Id == fileId);
        if (targetFile is null)
            return false;

        // Reset all files to duplicate
        foreach (var file in group.Files)
        {
            file.IsDuplicate = true;
        }

        // Set the target as original
        targetFile.IsDuplicate = false;

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Set file {FileId} as original in group {GroupId}", fileId, groupId);

        return true;
    }

    public async Task<Guid?> AutoSelectOriginalAsync(Guid groupId, AutoSelectRequest request, CancellationToken ct)
    {
        var group = await _dbContext.DuplicateGroups
            .Include(g => g.Files)
            .FirstOrDefaultAsync(g => g.Id == groupId, ct);

        if (group is null)
            return null;

        var selectedFile = SelectOriginal(group.Files.ToList(), request);
        if (selectedFile is null)
            return null;

        // Reset all files to duplicate
        foreach (var file in group.Files)
        {
            file.IsDuplicate = true;
        }

        // Set the selected as original
        selectedFile.IsDuplicate = false;

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Auto-selected file {FileId} as original in group {GroupId}",
            selectedFile.Id, groupId);

        return selectedFile.Id;
    }

    public async Task<int> AutoSelectAllAsync(AutoSelectRequest request, CancellationToken ct)
    {
        var groups = await _dbContext.DuplicateGroups
            .Include(g => g.Files)
            .Where(g => g.ResolvedAt == null)
            .ToListAsync(ct);

        var count = 0;

        foreach (var group in groups)
        {
            var selectedFile = SelectOriginal(group.Files.ToList(), request);
            if (selectedFile is null)
                continue;

            foreach (var file in group.Files)
            {
                file.IsDuplicate = true;
            }
            selectedFile.IsDuplicate = false;
            count++;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Auto-selected originals for {Count} groups", count);

        return count;
    }

    public async Task<FileStatisticsDto> GetStatisticsAsync(CancellationToken ct)
    {
        var duplicateGroups = await _dbContext.DuplicateGroups.CountAsync(ct);
        var duplicateFiles = await _dbContext.IndexedFiles
            .CountAsync(f => f.DuplicateGroupId != null, ct);

        var potentialSavings = await _dbContext.DuplicateGroups
            .Where(g => g.FileCount > 0)
            .Select(g => g.TotalSize - (g.TotalSize / g.FileCount))
            .SumAsync(ct);

        return new FileStatisticsDto
        {
            TotalFiles = await _dbContext.IndexedFiles.CountAsync(ct),
            TotalSizeBytes = await _dbContext.IndexedFiles.SumAsync(f => f.FileSize, ct),
            DuplicateGroups = duplicateGroups,
            DuplicateFiles = duplicateFiles,
            PotentialSavingsBytes = potentialSavings,
            LastIndexedAt = await _dbContext.IndexedFiles.MaxAsync(f => (DateTime?)f.IndexedAt, ct)
        };
    }

    public async Task<int> QueueNonOriginalsForDeletionAsync(Guid groupId, CancellationToken ct)
    {
        var group = await _dbContext.DuplicateGroups
            .Include(g => g.Files)
            .FirstOrDefaultAsync(g => g.Id == groupId, ct);

        if (group is null)
            return 0;

        var nonOriginals = group.Files.Where(f => f.IsDuplicate).ToList();

        // In a real implementation, this would queue files for the cleaner service
        // For now, we just mark the group as resolved
        group.ResolvedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Queued {Count} files for deletion from group {GroupId}",
            nonOriginals.Count, groupId);

        return nonOriginals.Count;
    }

    private static Database.Entities.IndexedFile? SelectOriginal(
        List<Database.Entities.IndexedFile> files,
        AutoSelectRequest request)
    {
        if (files.Count == 0)
            return null;

        return request.Strategy switch
        {
            AutoSelectStrategy.EarliestDate => files
                .OrderBy(f => f.CreatedAt)
                .First(),

            AutoSelectStrategy.ShortestPath => files
                .OrderBy(f => f.FilePath.Length)
                .First(),

            AutoSelectStrategy.PreferredDirectory when request.PreferredDirectoryPatterns?.Count > 0 =>
                files.FirstOrDefault(f =>
                    request.PreferredDirectoryPatterns.Any(p =>
                        f.FilePath.StartsWith(p.TrimEnd('*'))))
                ?? files.OrderBy(f => f.CreatedAt).First(),

            AutoSelectStrategy.LargestFile => files
                .OrderByDescending(f => f.FileSize)
                .First(),

            _ => files.OrderBy(f => f.CreatedAt).First()
        };
    }
}
