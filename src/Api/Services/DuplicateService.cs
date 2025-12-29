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
            Files = group.Files
                .Where(f => !f.IsHidden) // Filter out hidden files
                .Select(f => new IndexedFileDto
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
                DuplicateGroupId = f.DuplicateGroupId,
                IsHidden = f.IsHidden,
                HiddenCategory = f.HiddenCategory?.ToString(),
                HiddenAt = f.HiddenAt
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

    public async Task<DirectoryPatternDto?> GetPatternForGroupAsync(Guid groupId, CancellationToken ct)
    {
        var group = await _dbContext.DuplicateGroups
            .AsNoTracking()
            .Include(g => g.Files)
            .FirstOrDefaultAsync(g => g.Id == groupId, ct);

        if (group is null)
            return null;

        // Extract unique parent directories, sorted
        var directories = group.Files
            .Where(f => !f.IsHidden)
            .Select(f => Path.GetDirectoryName(f.FilePath) ?? "/")
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var patternHash = ComputePatternHash(directories);

        // Find all groups with same pattern
        var matchingGroupIds = await FindGroupsWithPatternAsync(directories, ct);

        // Calculate total potential savings
        var totalSavings = await _dbContext.DuplicateGroups
            .Where(g => matchingGroupIds.Contains(g.Id) && g.FileCount > 0)
            .Select(g => g.TotalSize - (g.TotalSize / g.FileCount))
            .SumAsync(ct);

        return new DirectoryPatternDto
        {
            Directories = directories,
            MatchingGroupCount = matchingGroupIds.Count,
            GroupIds = matchingGroupIds,
            PatternHash = patternHash,
            TotalPotentialSavings = totalSavings
        };
    }

    public async Task<ApplyPatternRuleResultDto> ApplyPatternRuleAsync(ApplyPatternRuleRequest request, CancellationToken ct)
    {
        var matchingGroupIds = await FindGroupsWithPatternAsync(request.Directories, ct);
        var currentPatternHash = ComputePatternHash(request.Directories);

        if (request.Preview)
        {
            // Preview mode: return counts without making changes
            return new ApplyPatternRuleResultDto
            {
                GroupsUpdated = matchingGroupIds.Count,
                GroupsSkipped = 0,
                FilesMarkedAsOriginal = matchingGroupIds.Count,
                NextUnresolvedGroupId = null
            };
        }

        var groups = await _dbContext.DuplicateGroups
            .Include(g => g.Files)
            .Where(g => matchingGroupIds.Contains(g.Id))
            .ToListAsync(ct);

        var updated = 0;
        var skipped = 0;
        var skippedReasons = new List<string>();

        foreach (var group in groups)
        {
            // Find files in the preferred directory (excluding hidden)
            var candidates = group.Files
                .Where(f => !f.IsHidden)
                .Where(f => (Path.GetDirectoryName(f.FilePath) ?? "/") == request.PreferredDirectory)
                .ToList();

            if (candidates.Count == 0)
            {
                skipped++;
                skippedReasons.Add($"Group {group.Id}: No file in preferred directory");
                continue;
            }

            // Select the best file based on tie-breaker
            var selectedFile = request.TieBreaker switch
            {
                PatternTieBreaker.EarliestDate => candidates.OrderBy(f => f.CreatedAt).First(),
                PatternTieBreaker.ShortestPath => candidates.OrderBy(f => f.FilePath.Length).First(),
                PatternTieBreaker.LargestFile => candidates.OrderByDescending(f => f.FileSize).First(),
                PatternTieBreaker.FirstIndexed => candidates.OrderBy(f => f.IndexedAt).First(),
                _ => candidates.First()
            };

            // Update all files in group
            foreach (var file in group.Files)
            {
                file.IsDuplicate = file.Id != selectedFile.Id;
            }

            group.KeptFileId = selectedFile.Id;
            group.Status = "auto-selected";
            updated++;
        }

        await _dbContext.SaveChangesAsync(ct);

        // Find next unresolved group with a DIFFERENT pattern
        var nextGroup = await FindNextUnresolvedGroupWithDifferentPatternAsync(currentPatternHash, ct);

        _logger.LogInformation(
            "Applied pattern rule to preferred directory '{PreferredDir}': {Updated} groups updated, {Skipped} skipped",
            request.PreferredDirectory, updated, skipped);

        return new ApplyPatternRuleResultDto
        {
            GroupsUpdated = updated,
            GroupsSkipped = skipped,
            FilesMarkedAsOriginal = updated,
            NextUnresolvedGroupId = nextGroup,
            SkippedGroupReasons = skippedReasons.Count > 0 ? skippedReasons : null
        };
    }

    private async Task<List<Guid>> FindGroupsWithPatternAsync(IReadOnlyList<string> targetDirectories, CancellationToken ct)
    {
        var targetSet = new HashSet<string>(targetDirectories);

        // Load all unresolved groups with files
        var groups = await _dbContext.DuplicateGroups
            .AsNoTracking()
            .Include(g => g.Files)
            .Where(g => g.Status != "cleaned")
            .ToListAsync(ct);

        return groups
            .Where(g =>
            {
                var groupDirs = g.Files
                    .Where(f => !f.IsHidden)
                    .Select(f => Path.GetDirectoryName(f.FilePath) ?? "/")
                    .Distinct()
                    .ToHashSet();
                return groupDirs.SetEquals(targetSet);
            })
            .Select(g => g.Id)
            .ToList();
    }

    private async Task<Guid?> FindNextUnresolvedGroupWithDifferentPatternAsync(string currentPatternHash, CancellationToken ct)
    {
        // Load all unresolved groups
        var groups = await _dbContext.DuplicateGroups
            .AsNoTracking()
            .Include(g => g.Files)
            .Where(g => g.Status == "pending" || g.Status == "conflict")
            .OrderBy(g => g.CreatedAt)
            .ToListAsync(ct);

        foreach (var group in groups)
        {
            var directories = group.Files
                .Where(f => !f.IsHidden)
                .Select(f => Path.GetDirectoryName(f.FilePath) ?? "/")
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            var patternHash = ComputePatternHash(directories);

            if (patternHash != currentPatternHash)
            {
                return group.Id;
            }
        }

        return null;
    }

    private static string ComputePatternHash(IEnumerable<string> directories)
    {
        var combined = string.Join("|", directories.OrderBy(d => d));
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task<GroupNavigationDto> GetNavigationAsync(Guid groupId, string? statusFilter, CancellationToken ct)
    {
        var query = _dbContext.DuplicateGroups.AsNoTracking();

        // Apply status filter if provided
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            query = query.Where(g => g.Status == statusFilter);
        }

        // Get ordered list of group IDs (same ordering as GetGroupsAsync)
        var orderedGroupIds = await query
            .OrderByDescending(g => g.TotalSize)
            .Select(g => g.Id)
            .ToListAsync(ct);

        var currentIndex = orderedGroupIds.IndexOf(groupId);

        if (currentIndex == -1)
        {
            // Group not found in filtered list
            return new GroupNavigationDto
            {
                PreviousGroupId = null,
                NextGroupId = null,
                CurrentPosition = 0,
                TotalGroups = orderedGroupIds.Count
            };
        }

        return new GroupNavigationDto
        {
            PreviousGroupId = currentIndex > 0 ? orderedGroupIds[currentIndex - 1] : null,
            NextGroupId = currentIndex < orderedGroupIds.Count - 1 ? orderedGroupIds[currentIndex + 1] : null,
            CurrentPosition = currentIndex + 1,
            TotalGroups = orderedGroupIds.Count
        };
    }
}
