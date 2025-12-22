using Database;
using Database.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Dtos;
using Shared.Requests;
using Shared.Responses;

namespace Api.Services;

/// <summary>
/// Implements smart selection algorithm for original files in duplicate groups.
/// </summary>
public class OriginalSelectionService : IOriginalSelectionService
{
    private readonly PhotosDbContext _dbContext;
    private readonly ILogger<OriginalSelectionService> _logger;

    // Default Synology-friendly path priorities
    private static readonly List<(string PathPrefix, int Priority)> DefaultPriorities =
    [
        ("/photos/", 100),
        ("/family/", 70),
        ("/albums/", 60),
        ("/public/", 10),
        ("/backup/", 5)
    ];

    public OriginalSelectionService(PhotosDbContext dbContext, ILogger<OriginalSelectionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<SelectionConfigDto> GetConfigAsync(CancellationToken ct)
    {
        var preferences = await GetPreferencesAsync(ct);

        return new SelectionConfigDto
        {
            PathPriorities = preferences,
            PreferExifData = true,
            PreferDeeperPaths = true,
            PreferOlderFiles = true,
            ConflictThreshold = 5
        };
    }

    public async Task<IReadOnlyList<SelectionPreferenceDto>> GetPreferencesAsync(CancellationToken ct)
    {
        var preferences = await _dbContext.SelectionPreferences
            .AsNoTracking()
            .OrderBy(p => p.SortOrder)
            .Select(p => new SelectionPreferenceDto
            {
                Id = p.Id,
                PathPrefix = p.PathPrefix,
                Priority = p.Priority,
                SortOrder = p.SortOrder
            })
            .ToListAsync(ct);

        // Return defaults if no preferences configured
        if (preferences.Count == 0)
        {
            return DefaultPriorities.Select((p, i) => new SelectionPreferenceDto
            {
                Id = Guid.Empty,
                PathPrefix = p.PathPrefix,
                Priority = p.Priority,
                SortOrder = i
            }).ToList();
        }

        return preferences;
    }

    public async Task SavePreferencesAsync(SavePreferencesRequest request, CancellationToken ct)
    {
        // Remove existing preferences
        var existing = await _dbContext.SelectionPreferences.ToListAsync(ct);
        _dbContext.SelectionPreferences.RemoveRange(existing);

        // Add new preferences
        var now = DateTime.UtcNow;
        var newPreferences = request.Preferences.Select((p, i) => new SelectionPreference
        {
            Id = p.Id == Guid.Empty ? Guid.NewGuid() : p.Id,
            PathPrefix = p.PathPrefix,
            Priority = Math.Clamp(p.Priority, 0, 100),
            SortOrder = i,
            CreatedAt = now,
            UpdatedAt = now
        });

        await _dbContext.SelectionPreferences.AddRangeAsync(newPreferences, ct);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Saved {Count} selection preferences", request.Preferences.Count);
    }

    public async Task ResetToDefaultsAsync(CancellationToken ct)
    {
        // Remove existing preferences
        var existing = await _dbContext.SelectionPreferences.ToListAsync(ct);
        _dbContext.SelectionPreferences.RemoveRange(existing);

        // Add default preferences
        var now = DateTime.UtcNow;
        var defaults = DefaultPriorities.Select((p, i) => new SelectionPreference
        {
            Id = Guid.NewGuid(),
            PathPrefix = p.PathPrefix,
            Priority = p.Priority,
            SortOrder = i,
            CreatedAt = now,
            UpdatedAt = now
        });

        await _dbContext.SelectionPreferences.AddRangeAsync(defaults, ct);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Reset selection preferences to defaults");
    }

    public async Task<RecalculateOriginalsResponse> RecalculateOriginalsAsync(RecalculateOriginalsRequest request, CancellationToken ct)
    {
        var config = await GetConfigAsync(ct);

        // Get groups to process
        var query = _dbContext.DuplicateGroups
            .Include(g => g.Files)
            .AsQueryable();

        if (request.Scope == "pending")
        {
            query = query.Where(g => g.Status == "pending" || g.Status == "conflict");
        }

        var groups = await query.ToListAsync(ct);

        var updated = 0;
        var conflicts = 0;
        var previewList = new List<DuplicateGroupDto>();

        foreach (var group in groups)
        {
            if (group.Files.Count == 0)
                continue;

            var result = SelectOriginalWithScoring(group.Files.ToList(), config);

            if (request.Preview)
            {
                // Preview mode - don't save changes
                previewList.Add(CreateGroupDto(group, result.SelectedFile?.Id, result.Status));
            }
            else
            {
                // Apply changes
                if (result.SelectedFile != null)
                {
                    // Reset all files to duplicate
                    foreach (var file in group.Files)
                    {
                        file.IsDuplicate = true;
                    }

                    // Set selected as original
                    result.SelectedFile.IsDuplicate = false;
                    group.KeptFileId = result.SelectedFile.Id;
                }

                group.Status = result.Status;

                if (result.Status == "conflict")
                {
                    conflicts++;
                }
                else
                {
                    updated++;
                }
            }
        }

        if (!request.Preview)
        {
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("Recalculated originals: {Updated} updated, {Conflicts} conflicts", updated, conflicts);
        }

        return new RecalculateOriginalsResponse
        {
            Updated = request.Preview ? groups.Count : updated,
            Conflicts = conflicts,
            Preview = request.Preview ? previewList : null
        };
    }

    public async Task<int> CalculateFileScoreAsync(Guid fileId, CancellationToken ct)
    {
        var file = await _dbContext.IndexedFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fileId, ct);

        if (file == null)
            return 0;

        var config = await GetConfigAsync(ct);
        return CalculateScore(file, config);
    }

    private SelectionResult SelectOriginalWithScoring(List<IndexedFile> files, SelectionConfigDto config)
    {
        if (files.Count == 0)
            return new SelectionResult(null, "pending");

        if (files.Count == 1)
            return new SelectionResult(files[0], "auto-selected");

        var scores = files
            .Select(f => new { File = f, Score = CalculateScore(f, config) })
            .OrderByDescending(x => x.Score)
            .ToList();

        var topScore = scores[0].Score;
        var runnerUpScore = scores[1].Score;

        // If scores are too close, mark as conflict (needs manual selection)
        if (topScore - runnerUpScore < config.ConflictThreshold)
        {
            return new SelectionResult(null, "conflict");
        }

        return new SelectionResult(scores[0].File, "auto-selected");
    }

    private int CalculateScore(IndexedFile file, SelectionConfigDto config)
    {
        var score = 0;

        // 1. Path priority (user-configured)
        foreach (var pref in config.PathPriorities.OrderBy(p => p.SortOrder))
        {
            if (file.FilePath.StartsWith(pref.PathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                score += pref.Priority;
                break; // Use first match only
            }
        }

        // 2. Path depth (deeper = more organized)
        if (config.PreferDeeperPaths)
        {
            var depth = file.FilePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;
            score += Math.Min(depth, 5) * 5; // Max +25
        }

        // 3. EXIF data presence
        if (config.PreferExifData)
        {
            // Check if file has metadata (dateTaken from EXIF, dimensions, etc.)
            if (file.Width > 0 && file.Height > 0)
            {
                score += 20;
            }
        }

        // 4. File age (older indexed = established)
        if (config.PreferOlderFiles)
        {
            var ageMonths = (int)((DateTime.UtcNow - file.IndexedAt).TotalDays / 30);
            score += Math.Min(ageMonths, 12); // Max +12
        }

        // 5. Tiebreaker: shorter path (more concise naming)
        score -= (int)(file.FilePath.Length * 0.01);

        return score;
    }

    private static DuplicateGroupDto CreateGroupDto(DuplicateGroup group, Guid? selectedFileId, string status)
    {
        return new DuplicateGroupDto
        {
            Id = group.Id,
            Hash = group.Hash,
            FileCount = group.FileCount,
            TotalSize = group.TotalSize,
            ResolvedAt = group.ResolvedAt,
            CreatedAt = group.CreatedAt,
            OriginalFileId = selectedFileId,
            Status = status,
            ValidatedAt = group.ValidatedAt,
            KeptFileId = selectedFileId,
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
                IsDuplicate = selectedFileId.HasValue ? f.Id != selectedFileId : f.IsDuplicate,
                DuplicateGroupId = f.DuplicateGroupId
            }).ToList()
        };
    }

    private record SelectionResult(IndexedFile? SelectedFile, string Status);
}
