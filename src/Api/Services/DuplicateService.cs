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

    public async Task<PagedResponse<DuplicateGroupDto>> GetGroupsAsync(int page, int pageSize, string? statusFilter, CancellationToken ct)
    {
        var query = _dbContext.DuplicateGroups.AsNoTracking();

        // Apply status filter if provided
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            query = query.Where(g => g.Status == statusFilter);
        }

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
                Files = new List<IndexedFileDto>(), // Don't load files in list view
                Status = g.Status,
                ValidatedAt = g.ValidatedAt,
                KeptFileId = g.KeptFileId
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
            Status = group.Status,
            ValidatedAt = group.ValidatedAt,
            KeptFileId = group.KeptFileId,
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

    public async Task<int> ValidateDuplicatesAsync(ValidateDuplicatesRequest request, CancellationToken ct)
    {
        var groups = await _dbContext.DuplicateGroups
            .Include(g => g.Files)
            .Where(g => request.GroupIds.Contains(g.Id))
            .ToListAsync(ct);

        var count = 0;

        foreach (var group in groups)
        {
            // Set the kept file if specified, otherwise use existing original
            var keptFileId = request.KeptFileId ?? group.Files.FirstOrDefault(f => !f.IsDuplicate)?.Id;

            if (keptFileId.HasValue)
            {
                // Mark all files as duplicates first
                foreach (var file in group.Files)
                {
                    file.IsDuplicate = true;
                }

                // Set the kept file as original
                var keptFile = group.Files.FirstOrDefault(f => f.Id == keptFileId.Value);
                if (keptFile != null)
                {
                    keptFile.IsDuplicate = false;
                }

                group.KeptFileId = keptFileId;
            }

            group.Status = "validated";
            group.ValidatedAt = DateTime.UtcNow;
            count++;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Validated {Count} duplicate groups", count);

        return count;
    }

    public async Task<ValidateBatchResponse> ValidateBatchAsync(ValidateBatchRequest request, CancellationToken ct)
    {
        // Default to "pending" if no filter provided
        var statusFilter = string.IsNullOrWhiteSpace(request.StatusFilter) ? "pending" : request.StatusFilter;

        var query = _dbContext.DuplicateGroups
            .Include(g => g.Files)
            .Where(g => g.Status == statusFilter)
            .OrderByDescending(g => g.TotalSize);

        var groups = await query
            .Take(request.Count)
            .ToListAsync(ct);

        var validated = 0;

        foreach (var group in groups)
        {
            // Use existing original as kept file if available
            var keptFile = group.Files.FirstOrDefault(f => !f.IsDuplicate);
            if (keptFile != null)
            {
                group.KeptFileId = keptFile.Id;
            }

            group.Status = "validated";
            group.ValidatedAt = DateTime.UtcNow;
            validated++;
        }

        await _dbContext.SaveChangesAsync(ct);

        // Count remaining groups with the same status filter
        var remaining = await _dbContext.DuplicateGroups
            .CountAsync(g => g.Status == statusFilter, ct);

        _logger.LogInformation("Batch validated {Validated} groups, {Remaining} remaining", validated, remaining);

        return new ValidateBatchResponse
        {
            Validated = validated,
            Remaining = remaining
        };
    }

    public async Task<int> UndoValidationAsync(UndoValidationRequest request, CancellationToken ct)
    {
        var groups = await _dbContext.DuplicateGroups
            .Where(g => request.GroupIds.Contains(g.Id))
            .ToListAsync(ct);

        var count = 0;

        foreach (var group in groups)
        {
            // Reset all validation fields
            group.Status = "pending";
            group.ValidatedAt = null;
            group.KeptFileId = null;
            count++;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Undid validation for {Count} groups", count);

        return count;
    }

    public async Task<DuplicateScanResultDto> ScanForDuplicatesAsync(CancellationToken ct)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInformation("Starting duplicate scan on all indexed files...");

        var totalFilesScanned = await _dbContext.IndexedFiles.CountAsync(ct);

        // Find all hashes that appear more than once (excluding null/empty hashes)
        var duplicateHashes = await _dbContext.IndexedFiles
            .Where(f => f.FileHash != null && f.FileHash != "")
            .GroupBy(f => f.FileHash)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToListAsync(ct);

        _logger.LogInformation("Found {Count} hashes with duplicates", duplicateHashes.Count);

        // Get existing groups by hash for quick lookup
        var existingGroups = await _dbContext.DuplicateGroups
            .ToDictionaryAsync(g => g.Hash, g => g, ct);

        var newGroupsCreated = 0;
        var groupsUpdated = 0;

        foreach (var hash in duplicateHashes)
        {
            if (string.IsNullOrEmpty(hash))
                continue;

            var files = await _dbContext.IndexedFiles
                .Where(f => f.FileHash == hash)
                .ToListAsync(ct);

            if (existingGroups.TryGetValue(hash, out var existingGroup))
            {
                // Update existing group
                existingGroup.FileCount = files.Count;
                existingGroup.TotalSize = files.Sum(f => f.FileSize);

                foreach (var file in files.Where(f => f.DuplicateGroupId != existingGroup.Id))
                {
                    file.DuplicateGroupId = existingGroup.Id;
                    file.IsDuplicate = true;
                }

                // Ensure at least one file is marked as original
                if (!files.Any(f => !f.IsDuplicate))
                {
                    files.First().IsDuplicate = false;
                }

                groupsUpdated++;
            }
            else
            {
                // Create new group
                var group = new Database.Entities.DuplicateGroup
                {
                    Id = Guid.NewGuid(),
                    Hash = hash,
                    FileCount = files.Count,
                    TotalSize = files.Sum(f => f.FileSize),
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.DuplicateGroups.Add(group);

                // Link files to group
                foreach (var file in files)
                {
                    file.DuplicateGroupId = group.Id;
                    file.IsDuplicate = true;
                }

                // Mark first file as original (by earliest indexed date)
                files.OrderBy(f => f.IndexedAt).First().IsDuplicate = false;

                newGroupsCreated++;
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        stopwatch.Stop();

        // Calculate final statistics
        var totalGroups = await _dbContext.DuplicateGroups.CountAsync(ct);
        var totalDuplicateFiles = await _dbContext.IndexedFiles
            .CountAsync(f => f.DuplicateGroupId != null, ct);
        var potentialSavings = await _dbContext.DuplicateGroups
            .Where(g => g.FileCount > 0)
            .Select(g => g.TotalSize - (g.TotalSize / g.FileCount))
            .SumAsync(ct);

        _logger.LogInformation(
            "Duplicate scan completed: {NewGroups} new groups, {UpdatedGroups} updated, {TotalGroups} total groups, {Duration}ms",
            newGroupsCreated, groupsUpdated, totalGroups, stopwatch.ElapsedMilliseconds);

        return new DuplicateScanResultDto
        {
            TotalFilesScanned = totalFilesScanned,
            NewGroupsCreated = newGroupsCreated,
            GroupsUpdated = groupsUpdated,
            TotalGroups = totalGroups,
            TotalDuplicateFiles = totalDuplicateFiles,
            PotentialSavingsBytes = potentialSavings,
            ScanDurationMs = stopwatch.ElapsedMilliseconds
        };
    }
}
