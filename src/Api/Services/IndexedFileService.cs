using Database;
using Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shared.Dtos;
using Shared.Requests;
using Shared.Responses;

namespace Api.Services;

/// <summary>
/// Service implementation for managing indexed files.
/// </summary>
public class IndexedFileService : IIndexedFileService
{
    private readonly PhotosDbContext _dbContext;
    private readonly ILogger<IndexedFileService> _logger;
    private readonly string _thumbnailDirectory;

    public IndexedFileService(PhotosDbContext dbContext, ILogger<IndexedFileService> logger, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _logger = logger;
        _thumbnailDirectory = configuration.GetValue<string>("ThumbnailDirectory")
            ?? Path.Combine(Path.GetTempPath(), "photos-index-thumbnails");
    }

    public async Task<PagedResponse<IndexedFileDto>> QueryAsync(FileQueryParameters query, CancellationToken ct)
    {
        var dbQuery = _dbContext.IndexedFiles.AsNoTracking();

        // Apply filters
        if (query.DirectoryId.HasValue)
        {
            // Note: We'd need a ScanDirectoryId on IndexedFile for this
            // For now, we'll filter by path prefix from the directory
        }

        if (query.HasDuplicates == true)
        {
            dbQuery = dbQuery.Where(f => f.DuplicateGroupId != null);
        }
        else if (query.HasDuplicates == false)
        {
            dbQuery = dbQuery.Where(f => f.DuplicateGroupId == null);
        }

        if (query.MinDate.HasValue)
        {
            dbQuery = dbQuery.Where(f => f.CreatedAt >= query.MinDate.Value);
        }

        if (query.MaxDate.HasValue)
        {
            dbQuery = dbQuery.Where(f => f.CreatedAt <= query.MaxDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            // Parse search prefixes: path:, taken:, modified:, created:
            if (search.StartsWith("path:", StringComparison.OrdinalIgnoreCase))
            {
                // Filter by file path (contains match)
                var pathFilter = search[5..].Trim().ToLower();
                dbQuery = dbQuery.Where(f => f.FilePath.ToLower().Contains(pathFilter));
            }
            else if (search.StartsWith("taken:", StringComparison.OrdinalIgnoreCase))
            {
                // Filter by DateTaken (exact day match)
                var dateStr = search[6..].Trim();
                if (TryParseDate(dateStr, out var date))
                {
                    var startOfDay = date.Date;
                    var endOfDay = date.Date.AddDays(1);
                    dbQuery = dbQuery.Where(f => f.DateTaken >= startOfDay && f.DateTaken < endOfDay);
                }
                else
                {
                    // Invalid date format - return empty results
                    dbQuery = dbQuery.Where(f => false);
                }
            }
            else if (search.StartsWith("modified:", StringComparison.OrdinalIgnoreCase))
            {
                // Filter by ModifiedAt (exact day match)
                var dateStr = search[9..].Trim();
                if (TryParseDate(dateStr, out var date))
                {
                    var startOfDay = date.Date;
                    var endOfDay = date.Date.AddDays(1);
                    dbQuery = dbQuery.Where(f => f.ModifiedAt >= startOfDay && f.ModifiedAt < endOfDay);
                }
                else
                {
                    // Invalid date format - return empty results
                    dbQuery = dbQuery.Where(f => false);
                }
            }
            else if (search.StartsWith("created:", StringComparison.OrdinalIgnoreCase))
            {
                // Filter by CreatedAt (exact day match)
                var dateStr = search[8..].Trim();
                if (TryParseDate(dateStr, out var date))
                {
                    var startOfDay = date.Date;
                    var endOfDay = date.Date.AddDays(1);
                    dbQuery = dbQuery.Where(f => f.CreatedAt >= startOfDay && f.CreatedAt < endOfDay);
                }
                else
                {
                    // Invalid date format - return empty results
                    dbQuery = dbQuery.Where(f => false);
                }
            }
            else
            {
                // Default: search by filename
                var searchLower = search.ToLower();
                dbQuery = dbQuery.Where(f => f.FileName.ToLower().Contains(searchLower));
            }
        }

        // Get total count
        var totalItems = await dbQuery.CountAsync(ct);

        // Apply sorting
        dbQuery = query.SortBy switch
        {
            FileSortBy.Name => query.SortDescending
                ? dbQuery.OrderByDescending(f => f.FileName)
                : dbQuery.OrderBy(f => f.FileName),
            FileSortBy.Size => query.SortDescending
                ? dbQuery.OrderByDescending(f => f.FileSize)
                : dbQuery.OrderBy(f => f.FileSize),
            FileSortBy.CreatedAt => query.SortDescending
                ? dbQuery.OrderByDescending(f => f.CreatedAt)
                : dbQuery.OrderBy(f => f.CreatedAt),
            FileSortBy.ModifiedAt => query.SortDescending
                ? dbQuery.OrderByDescending(f => f.ModifiedAt)
                : dbQuery.OrderBy(f => f.ModifiedAt),
            _ => query.SortDescending
                ? dbQuery.OrderByDescending(f => f.IndexedAt)
                : dbQuery.OrderBy(f => f.IndexedAt)
        };

        // Apply pagination
        var items = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(f => MapToDto(f))
            .ToListAsync(ct);

        return new PagedResponse<IndexedFileDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalItems = totalItems
        };
    }

    public async Task<IndexedFileDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var entity = await _dbContext.IndexedFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, ct);

        return entity is null ? null : MapToDto(entity);
    }

    public async Task<byte[]?> GetThumbnailAsync(Guid id, CancellationToken ct)
    {
        var entity = await _dbContext.IndexedFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, ct);

        if (entity?.ThumbnailPath is null)
            return null;

        try
        {
            if (File.Exists(entity.ThumbnailPath))
            {
                return await File.ReadAllBytesAsync(entity.ThumbnailPath, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read thumbnail: {Path}", entity.ThumbnailPath);
        }

        return null;
    }

    public async Task<(byte[] Content, string FileName, string ContentType)?> DownloadFileAsync(Guid id, CancellationToken ct)
    {
        var entity = await _dbContext.IndexedFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, ct);

        if (entity is null)
            return null;

        try
        {
            if (!File.Exists(entity.FilePath))
            {
                _logger.LogWarning("File not found on disk: {Path}", entity.FilePath);
                return null;
            }

            var content = await File.ReadAllBytesAsync(entity.FilePath, ct);
            var contentType = GetContentType(entity.FileName);

            return (content, entity.FileName, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read file: {Path}", entity.FilePath);
            return null;
        }
    }

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".tiff" or ".tif" => "image/tiff",
            ".heic" => "image/heic",
            _ => "application/octet-stream"
        };
    }

    private async Task<string?> SaveThumbnailAsync(Guid fileId, string base64Data, CancellationToken ct)
    {
        try
        {
            // Ensure thumbnail directory exists
            if (!Directory.Exists(_thumbnailDirectory))
            {
                Directory.CreateDirectory(_thumbnailDirectory);
            }

            var bytes = Convert.FromBase64String(base64Data);
            var thumbnailPath = Path.Combine(_thumbnailDirectory, $"{fileId}.jpg");

            await File.WriteAllBytesAsync(thumbnailPath, bytes, ct);

            _logger.LogDebug("Saved thumbnail for file {FileId} to {Path}", fileId, thumbnailPath);

            return thumbnailPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save thumbnail for file {FileId}", fileId);
            return null;
        }
    }

    public async Task<BatchOperationResponse> BatchIngestAsync(BatchIngestFilesRequest request, CancellationToken ct)
    {
        var succeeded = 0;
        var failed = 0;
        var errors = new List<BatchOperationError>();

        foreach (var file in request.Files)
        {
            try
            {
                // Validate file size - reject 0-byte files
                if (file.FileSize <= 0)
                {
                    failed++;
                    errors.Add(new BatchOperationError
                    {
                        Item = file.FilePath,
                        Error = "File size is zero or negative"
                    });
                    _logger.LogWarning("Rejecting file with invalid size {Size}: {Path}", file.FileSize, file.FilePath);
                    continue;
                }

                // Check if file already exists by path
                var existing = await _dbContext.IndexedFiles
                    .FirstOrDefaultAsync(f => f.FilePath == file.FilePath, ct);

                if (existing is not null)
                {
                    // Update existing
                    existing.FileHash = file.FileHash;
                    existing.FileSize = file.FileSize;
                    existing.Width = file.Width;
                    existing.Height = file.Height;
                    existing.ModifiedAt = file.ModifiedAt;
                    existing.IndexedAt = DateTime.UtcNow;
                    existing.DateTaken = file.DateTaken;
                    existing.CameraMake = file.CameraMake;
                    existing.CameraModel = file.CameraModel;
                    existing.GpsLatitude = file.GpsLatitude;
                    existing.GpsLongitude = file.GpsLongitude;
                    existing.Iso = file.Iso;
                    existing.Aperture = file.Aperture;
                    existing.ShutterSpeed = file.ShutterSpeed;

                    // Update thumbnail if provided
                    if (!string.IsNullOrEmpty(file.ThumbnailBase64))
                    {
                        var thumbnailPath = await SaveThumbnailAsync(existing.Id, file.ThumbnailBase64, ct);
                        if (thumbnailPath != null)
                        {
                            existing.ThumbnailPath = thumbnailPath;
                        }
                    }
                }
                else
                {
                    // Create new
                    var entityId = Guid.NewGuid();
                    string? thumbnailPath = null;

                    // Save thumbnail if provided
                    if (!string.IsNullOrEmpty(file.ThumbnailBase64))
                    {
                        thumbnailPath = await SaveThumbnailAsync(entityId, file.ThumbnailBase64, ct);
                    }

                    var entity = new IndexedFile
                    {
                        Id = entityId,
                        FilePath = file.FilePath,
                        FileName = file.FileName,
                        FileHash = file.FileHash,
                        FileSize = file.FileSize,
                        Width = file.Width,
                        Height = file.Height,
                        CreatedAt = file.CreatedAt ?? DateTime.UtcNow,
                        ModifiedAt = file.ModifiedAt,
                        IndexedAt = DateTime.UtcNow,
                        ThumbnailPath = thumbnailPath,
                        DateTaken = file.DateTaken,
                        CameraMake = file.CameraMake,
                        CameraModel = file.CameraModel,
                        GpsLatitude = file.GpsLatitude,
                        GpsLongitude = file.GpsLongitude,
                        Iso = file.Iso,
                        Aperture = file.Aperture,
                        ShutterSpeed = file.ShutterSpeed
                    };
                    _dbContext.IndexedFiles.Add(entity);
                }

                succeeded++;
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add(new BatchOperationError
                {
                    Item = file.FilePath,
                    Error = ex.Message
                });
                _logger.LogWarning(ex, "Failed to ingest file: {Path}", file.FilePath);
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        // Detect duplicates by hash
        await DetectDuplicatesAsync(ct);

        _logger.LogInformation("Batch ingest completed: {Succeeded} succeeded, {Failed} failed",
            succeeded, failed);

        return new BatchOperationResponse
        {
            TotalRequested = request.Files.Count,
            Succeeded = succeeded,
            Failed = failed,
            Errors = errors.Count > 0 ? errors : null
        };
    }

    public async Task<FileStatisticsDto> GetStatisticsAsync(CancellationToken ct)
    {
        var totalFiles = await _dbContext.IndexedFiles.CountAsync(ct);
        var totalSize = await _dbContext.IndexedFiles.SumAsync(f => f.FileSize, ct);

        var duplicateGroups = await _dbContext.DuplicateGroups.CountAsync(ct);
        var duplicateFiles = await _dbContext.IndexedFiles
            .CountAsync(f => f.DuplicateGroupId != null, ct);

        // Calculate potential savings (size of all duplicates minus one copy per group)
        var potentialSavings = await _dbContext.DuplicateGroups
            .Select(g => g.TotalSize - (g.TotalSize / g.FileCount))
            .SumAsync(ct);

        var lastIndexed = await _dbContext.IndexedFiles
            .MaxAsync(f => (DateTime?)f.IndexedAt, ct);

        return new FileStatisticsDto
        {
            TotalFiles = totalFiles,
            TotalSizeBytes = totalSize,
            DuplicateGroups = duplicateGroups,
            DuplicateFiles = duplicateFiles,
            PotentialSavingsBytes = potentialSavings,
            LastIndexedAt = lastIndexed
        };
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken ct)
    {
        var entity = await _dbContext.IndexedFiles.FindAsync([id], ct);

        if (entity is null)
            return false;

        // For soft delete, we could add a DeletedAt column
        // For now, we'll do a hard delete
        _dbContext.IndexedFiles.Remove(entity);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted file {Id}", id);

        return true;
    }

    private async Task DetectDuplicatesAsync(CancellationToken ct)
    {
        // Find all files with duplicate hashes
        var duplicateHashes = await _dbContext.IndexedFiles
            .GroupBy(f => f.FileHash)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToListAsync(ct);

        foreach (var hash in duplicateHashes)
        {
            // Check if group already exists
            var existingGroup = await _dbContext.DuplicateGroups
                .FirstOrDefaultAsync(g => g.Hash == hash, ct);

            if (existingGroup is null)
            {
                // Create new group
                var files = await _dbContext.IndexedFiles
                    .Where(f => f.FileHash == hash)
                    .ToListAsync(ct);

                var group = new DuplicateGroup
                {
                    Id = Guid.NewGuid(),
                    Hash = hash,
                    FileCount = files.Count,
                    TotalSize = files.Sum(f => f.FileSize),
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.DuplicateGroups.Add(group);

                // Link files to group
                foreach (var file in files)
                {
                    file.DuplicateGroupId = group.Id;
                    file.IsDuplicate = true;
                }

                // Mark first file as original
                files.First().IsDuplicate = false;
            }
            else
            {
                // Update existing group
                var files = await _dbContext.IndexedFiles
                    .Where(f => f.FileHash == hash)
                    .ToListAsync(ct);

                existingGroup.FileCount = files.Count;
                existingGroup.TotalSize = files.Sum(f => f.FileSize);

                foreach (var file in files.Where(f => f.DuplicateGroupId != existingGroup.Id))
                {
                    file.DuplicateGroupId = existingGroup.Id;
                    file.IsDuplicate = true;
                }
            }
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FileNeedsReindexDto>> CheckNeedsReindexAsync(
        CheckFilesNeedReindexRequest request,
        CancellationToken ct)
    {
        var filePaths = request.Files.Select(f => f.FilePath).ToList();

        // Query database for existing files
        var existingFiles = await _dbContext.IndexedFiles
            .AsNoTracking()
            .Where(f => filePaths.Contains(f.FilePath))
            .ToDictionaryAsync(f => f.FilePath, f => f.IndexedAt, ct);

        var results = new List<FileNeedsReindexDto>();

        foreach (var file in request.Files)
        {
            bool needsReindex;

            if (existingFiles.TryGetValue(file.FilePath, out var indexedAt))
            {
                // File exists in database - check if modified after last index
                needsReindex = file.ModifiedAt > indexedAt;
            }
            else
            {
                // File not found in database - needs indexing
                needsReindex = true;
            }

            results.Add(new FileNeedsReindexDto
            {
                FilePath = file.FilePath,
                LastModifiedAt = file.ModifiedAt,
                NeedsReindex = needsReindex
            });
        }

        _logger.LogInformation(
            "Checked {Total} files for reindex: {NeedsReindex} need reindexing",
            results.Count,
            results.Count(r => r.NeedsReindex));

        return results;
    }

    public async Task<IReadOnlyList<IndexedFileDto>> GetBatchMetadataAsync(IReadOnlyList<Guid> fileIds, CancellationToken ct)
    {
        var entities = await _dbContext.IndexedFiles
            .AsNoTracking()
            .Where(f => fileIds.Contains(f.Id))
            .ToListAsync(ct);

        return entities.Select(MapToDto).ToList();
    }

    private static IndexedFileDto MapToDto(IndexedFile entity) => new()
    {
        Id = entity.Id,
        FilePath = entity.FilePath,
        FileName = entity.FileName,
        FileHash = entity.FileHash,
        FileSize = entity.FileSize,
        Width = entity.Width,
        Height = entity.Height,
        CreatedAt = entity.CreatedAt,
        ModifiedAt = entity.ModifiedAt,
        IndexedAt = entity.IndexedAt,
        ThumbnailPath = entity.ThumbnailPath,
        IsDuplicate = entity.IsDuplicate,
        DuplicateGroupId = entity.DuplicateGroupId,
        DateTaken = entity.DateTaken,
        CameraMake = entity.CameraMake,
        CameraModel = entity.CameraModel,
        GpsLatitude = entity.GpsLatitude,
        GpsLongitude = entity.GpsLongitude,
        Iso = entity.Iso,
        Aperture = entity.Aperture,
        ShutterSpeed = entity.ShutterSpeed,
        LastError = entity.LastError,
        RetryCount = entity.RetryCount
    };

    /// <summary>
    /// Parses a date string supporting yyyy-MM-dd and dd-MM-yyyy formats.
    /// </summary>
    private static bool TryParseDate(string dateStr, out DateTime result)
    {
        // Try yyyy-MM-dd format first (ISO 8601)
        if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out result))
        {
            return true;
        }

        // Try dd-MM-yyyy format (European)
        if (DateTime.TryParseExact(dateStr, "dd-MM-yyyy",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out result))
        {
            return true;
        }

        result = default;
        return false;
    }
}
