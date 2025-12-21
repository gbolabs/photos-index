using Database;
using Database.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Dtos;
using Shared.Requests;
using Shared.Responses;

namespace Api.Services;

/// <summary>
/// Service implementation for managing scan directories.
/// </summary>
public class ScanDirectoryService : IScanDirectoryService
{
    private readonly PhotosDbContext _dbContext;
    private readonly ILogger<ScanDirectoryService> _logger;

    public ScanDirectoryService(PhotosDbContext dbContext, ILogger<ScanDirectoryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PagedResponse<ScanDirectoryDto>> GetAllAsync(int page, int pageSize, CancellationToken ct)
    {
        var query = _dbContext.ScanDirectories.AsNoTracking();

        var totalItems = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => MapToDto(d))
            .ToListAsync(ct);

        return new PagedResponse<ScanDirectoryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems
        };
    }

    public async Task<ScanDirectoryDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var entity = await _dbContext.ScanDirectories
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        return entity is null ? null : MapToDto(entity);
    }

    public async Task<ScanDirectoryDto> CreateAsync(CreateScanDirectoryRequest request, CancellationToken ct)
    {
        var entity = new ScanDirectory
        {
            Id = Guid.NewGuid(),
            Path = request.Path,
            IsEnabled = request.IsEnabled,
            CreatedAt = DateTime.UtcNow,
            FileCount = 0
        };

        _dbContext.ScanDirectories.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created scan directory {Id} for path {Path}", entity.Id, entity.Path);

        return MapToDto(entity);
    }

    public async Task<ScanDirectoryDto?> UpdateAsync(Guid id, UpdateScanDirectoryRequest request, CancellationToken ct)
    {
        var entity = await _dbContext.ScanDirectories.FindAsync([id], ct);

        if (entity is null)
            return null;

        if (request.Path is not null)
            entity.Path = request.Path;

        if (request.IsEnabled.HasValue)
            entity.IsEnabled = request.IsEnabled.Value;

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Updated scan directory {Id}", id);

        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var entity = await _dbContext.ScanDirectories.FindAsync([id], ct);

        if (entity is null)
            return false;

        _dbContext.ScanDirectories.Remove(entity);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted scan directory {Id}", id);

        return true;
    }

    public async Task<bool> TriggerScanAsync(Guid id, CancellationToken ct)
    {
        var entity = await _dbContext.ScanDirectories.FindAsync([id], ct);

        if (entity is null)
            return false;

        // In a real implementation, this would publish a message to a queue
        // For now, we just log the intent
        _logger.LogInformation("Scan triggered for directory {Id} at path {Path}", id, entity.Path);

        return true;
    }

    public async Task<bool> PathExistsAsync(string path, CancellationToken ct)
    {
        return await _dbContext.ScanDirectories
            .AnyAsync(d => d.Path == path, ct);
    }

    public async Task<bool> UpdateLastScannedAsync(Guid id, CancellationToken ct)
    {
        var entity = await _dbContext.ScanDirectories.FindAsync([id], ct);

        if (entity is null)
            return false;

        entity.LastScannedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Updated last scanned timestamp for directory {Id}", id);

        return true;
    }

    private static ScanDirectoryDto MapToDto(ScanDirectory entity) => new()
    {
        Id = entity.Id,
        Path = entity.Path,
        IsEnabled = entity.IsEnabled,
        LastScannedAt = entity.LastScannedAt,
        CreatedAt = entity.CreatedAt,
        FileCount = entity.FileCount
    };
}
