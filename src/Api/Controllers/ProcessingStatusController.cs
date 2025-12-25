using Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/processing")]
[Produces("application/json")]
public class ProcessingStatusController : ControllerBase
{
    private readonly PhotosDbContext _dbContext;
    private readonly ILogger<ProcessingStatusController> _logger;

    public ProcessingStatusController(
        PhotosDbContext dbContext,
        ILogger<ProcessingStatusController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Get processing status statistics.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(ProcessingStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProcessingStatusDto>> GetStatus(CancellationToken ct = default)
    {
        var totalFiles = await _dbContext.IndexedFiles.CountAsync(ct);
        var filesWithThumbnails = await _dbContext.IndexedFiles
            .CountAsync(f => f.ThumbnailPath != null, ct);
        var filesWithMetadata = await _dbContext.IndexedFiles
            .CountAsync(f => f.Width != null && f.Height != null, ct);
        var filesPendingProcessing = await _dbContext.IndexedFiles
            .CountAsync(f => f.ThumbnailPath == null || f.Width == null, ct);
        var filesWithErrors = await _dbContext.IndexedFiles
            .CountAsync(f => f.LastError != null, ct);

        var status = new ProcessingStatusDto
        {
            TotalFiles = totalFiles,
            FilesWithThumbnails = filesWithThumbnails,
            FilesWithMetadata = filesWithMetadata,
            FilesPendingProcessing = filesPendingProcessing,
            FilesWithErrors = filesWithErrors,
            ThumbnailProgress = totalFiles > 0 ? (double)filesWithThumbnails / totalFiles * 100 : 0,
            MetadataProgress = totalFiles > 0 ? (double)filesWithMetadata / totalFiles * 100 : 0
        };

        return Ok(status);
    }

    /// <summary>
    /// Get files that failed processing.
    /// </summary>
    [HttpGet("failed")]
    [ProducesResponseType(typeof(IReadOnlyList<FailedFileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FailedFileDto>>> GetFailedFiles(
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        var failedFiles = await _dbContext.IndexedFiles
            .Where(f => f.LastError != null)
            .OrderByDescending(f => f.IndexedAt)
            .Take(limit)
            .Select(f => new FailedFileDto
            {
                Id = f.Id,
                FilePath = f.FilePath,
                FileName = f.FileName,
                LastError = f.LastError!,
                RetryCount = f.RetryCount,
                IndexedAt = f.IndexedAt
            })
            .ToListAsync(ct);

        return Ok(failedFiles);
    }

    /// <summary>
    /// Retry processing for failed files.
    /// </summary>
    [HttpPost("retry")]
    [ProducesResponseType(typeof(RetryResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RetryResultDto>> RetryFailedFiles(
        [FromQuery] int maxFiles = 50,
        CancellationToken ct = default)
    {
        var filesToRetry = await _dbContext.IndexedFiles
            .Where(f => f.LastError != null && f.RetryCount < 3)
            .OrderBy(f => f.RetryCount)
            .Take(maxFiles)
            .ToListAsync(ct);

        foreach (var file in filesToRetry)
        {
            file.LastError = null;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Cleared error status for {Count} files for retry", filesToRetry.Count);

        return Ok(new RetryResultDto
        {
            FilesQueued = filesToRetry.Count,
            Message = $"Cleared error status for {filesToRetry.Count} files"
        });
    }
}

public record ProcessingStatusDto
{
    public int TotalFiles { get; init; }
    public int FilesWithThumbnails { get; init; }
    public int FilesWithMetadata { get; init; }
    public int FilesPendingProcessing { get; init; }
    public int FilesWithErrors { get; init; }
    public double ThumbnailProgress { get; init; }
    public double MetadataProgress { get; init; }
}

public record FailedFileDto
{
    public Guid Id { get; init; }
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required string LastError { get; init; }
    public int RetryCount { get; init; }
    public DateTime IndexedAt { get; init; }
}

public record RetryResultDto
{
    public int FilesQueued { get; init; }
    public required string Message { get; init; }
}
