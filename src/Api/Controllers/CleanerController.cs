using Api.Hubs;
using Api.Services;
using Database;
using Database.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Shared.Dtos;
using Shared.Storage;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CleanerController : ControllerBase
{
    private readonly PhotosDbContext _db;
    private readonly IHubContext<CleanerHub, ICleanerClient> _hubContext;
    private readonly IObjectStorage _objectStorage;
    private readonly ICleanerJobService _jobService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CleanerController> _logger;

    public CleanerController(
        PhotosDbContext db,
        IHubContext<CleanerHub, ICleanerClient> hubContext,
        IObjectStorage objectStorage,
        ICleanerJobService jobService,
        IConfiguration configuration,
        ILogger<CleanerController> logger)
    {
        _db = db;
        _hubContext = hubContext;
        _objectStorage = objectStorage;
        _jobService = jobService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Create a new cleaner job to delete the specified files.
    /// </summary>
    [HttpPost("jobs")]
    [ProducesResponseType(typeof(CreateCleanerJobResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateCleanerJobResult>> CreateJob(
        [FromBody] DeleteFilesRequest request,
        CancellationToken ct)
    {
        if (request.FileIds.Count == 0)
        {
            return BadRequest("No files specified for deletion");
        }

        if (!CleanerHub.HasConnectedCleaner())
        {
            return BadRequest("No cleaner service is connected. Cannot process delete requests.");
        }

        // Get file information from database
        var files = await _db.IndexedFiles
            .Where(f => request.FileIds.Contains(f.Id))
            .Select(f => new { f.Id, f.FilePath, f.FileHash, f.FileSize })
            .ToListAsync(ct);

        if (files.Count == 0)
        {
            return BadRequest("None of the specified files exist in the database");
        }

        // Create the job
        var job = new CleanerJob
        {
            Id = Guid.NewGuid(),
            Status = CleanerJobStatus.Pending,
            Category = request.Category,
            DryRun = request.DryRun,
            TotalFiles = files.Count,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var file in files)
        {
            job.Files.Add(new CleanerJobFile
            {
                Id = Guid.NewGuid(),
                CleanerJobId = job.Id,
                FileId = file.Id,
                FilePath = file.FilePath,
                FileHash = file.FileHash,
                FileSize = file.FileSize,
                Status = CleanerFileStatus.Pending
            });
        }

        _db.CleanerJobs.Add(job);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created cleaner job {JobId} with {FileCount} files, DryRun={DryRun}",
            job.Id, files.Count, request.DryRun);

        // Queue the job for processing
        await _jobService.QueueJobAsync(job.Id, ct);

        return CreatedAtAction(nameof(GetJob), new { id = job.Id }, new CreateCleanerJobResult
        {
            JobId = job.Id,
            FileCount = files.Count,
            DryRun = request.DryRun
        });
    }

    /// <summary>
    /// Get a cleaner job by ID.
    /// </summary>
    [HttpGet("jobs/{id:guid}")]
    [ProducesResponseType(typeof(CleanerJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CleanerJobDto>> GetJob(Guid id, CancellationToken ct)
    {
        var job = await _db.CleanerJobs
            .Include(j => j.Files)
            .FirstOrDefaultAsync(j => j.Id == id, ct);

        if (job == null)
        {
            return NotFound();
        }

        return Ok(MapToDto(job));
    }

    /// <summary>
    /// Get recent cleaner jobs.
    /// </summary>
    [HttpGet("jobs")]
    [ProducesResponseType(typeof(IReadOnlyList<CleanerJobDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CleanerJobDto>>> GetJobs(
        [FromQuery] int limit = 20,
        [FromQuery] CleanerJobStatus? status = null,
        CancellationToken ct = default)
    {
        var query = _db.CleanerJobs.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(j => j.Status == status.Value);
        }

        var jobs = await query
            .OrderByDescending(j => j.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        return Ok(jobs.Select(MapToDto).ToList());
    }

    /// <summary>
    /// Cancel a cleaner job.
    /// </summary>
    [HttpPost("jobs/{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelJob(Guid id, CancellationToken ct)
    {
        var job = await _db.CleanerJobs.FindAsync([id], ct);
        if (job == null)
        {
            return NotFound();
        }

        if (job.Status == CleanerJobStatus.Completed || job.Status == CleanerJobStatus.Cancelled)
        {
            return BadRequest("Job is already completed or cancelled");
        }

        job.Status = CleanerJobStatus.Cancelled;
        job.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Notify connected cleaners
        await _hubContext.Clients.All.CancelJob(id);

        _logger.LogInformation("Cancelled cleaner job {JobId}", id);

        return NoContent();
    }

    /// <summary>
    /// Archive a file uploaded from the CleanerService.
    /// </summary>
    [HttpPost("archive")]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100 MB limit
    [ProducesResponseType(typeof(ArchiveFileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ArchiveFileResult>> ArchiveFile(
        [FromForm] Guid jobId,
        [FromForm] Guid fileId,
        [FromForm] string category,
        [FromForm] string originalPath,
        [FromForm] string fileHash,
        IFormFile file,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded");
        }

        if (!Enum.TryParse<DeleteCategory>(category, true, out var deleteCategory))
        {
            return BadRequest($"Invalid category: {category}");
        }

        // Generate archive path based on category and date
        var now = DateTime.UtcNow;
        var fileName = Path.GetFileName(originalPath);
        var extension = Path.GetExtension(fileName);
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var archiveFileName = $"{nameWithoutExt}_{fileId:N}_{fileHash[..8]}{extension}";
        var archivePath = $"{category.ToLowerInvariant()}/{now:yyyy-MM}/{archiveFileName}";

        // Get archive bucket from configuration
        var archiveBucket = _configuration["Minio:ArchiveBucket"] ?? "archive";

        _logger.LogInformation("Archiving file {FileId} to {Bucket}/{ArchivePath}", fileId, archiveBucket, archivePath);

        // Ensure bucket exists
        await _objectStorage.EnsureBucketExistsAsync(archiveBucket);

        // Upload to MinIO
        await using var stream = file.OpenReadStream();
        await _objectStorage.UploadAsync(archiveBucket, archivePath, stream, file.ContentType);

        // Update the job file record
        var jobFile = await _db.CleanerJobFiles
            .FirstOrDefaultAsync(f => f.CleanerJobId == jobId && f.FileId == fileId, ct);

        if (jobFile != null)
        {
            jobFile.Status = CleanerFileStatus.Uploaded;
            jobFile.ArchivePath = $"{archiveBucket}/{archivePath}";
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new ArchiveFileResult
        {
            Success = true,
            ArchivePath = $"{archiveBucket}/{archivePath}"
        });
    }

    /// <summary>
    /// Confirm that a file has been deleted by the CleanerService.
    /// </summary>
    [HttpPost("confirm-delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmDelete(
        [FromBody] DeleteFileResult result,
        CancellationToken ct)
    {
        var jobFile = await _db.CleanerJobFiles
            .Include(f => f.IndexedFile)
            .FirstOrDefaultAsync(f => f.CleanerJobId == result.JobId && f.FileId == result.FileId, ct);

        if (jobFile == null)
        {
            return NotFound();
        }

        jobFile.Status = result.Success ? CleanerFileStatus.Deleted : CleanerFileStatus.Failed;
        jobFile.ErrorMessage = result.Error;
        jobFile.ProcessedAt = DateTime.UtcNow;

        // Update the indexed file if deletion was successful
        if (result.Success && !result.WasDryRun && jobFile.IndexedFile != null)
        {
            jobFile.IndexedFile.IsDeleted = true;
            jobFile.IndexedFile.DeletedAt = DateTime.UtcNow;
            jobFile.IndexedFile.ArchivePath = result.ArchivePath;

            // Also delete the thumbnail from MinIO
            if (!string.IsNullOrEmpty(jobFile.IndexedFile.ThumbnailPath))
            {
                await DeleteThumbnailAsync(jobFile.IndexedFile.ThumbnailPath, ct);
                jobFile.IndexedFile.ThumbnailPath = null;
            }
        }

        // Update job progress
        var job = await _db.CleanerJobs.FindAsync([result.JobId], ct);
        if (job != null)
        {
            job.ProcessedFiles++;
            if (result.Success)
                job.SucceededFiles++;
            else
                job.FailedFiles++;

            // Check if job is complete
            if (job.ProcessedFiles >= job.TotalFiles)
            {
                job.Status = job.FailedFiles > 0 ? CleanerJobStatus.Failed : CleanerJobStatus.Completed;
                job.CompletedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Delete confirmed: Job={JobId}, File={FileId}, Success={Success}, DryRun={DryRun}",
            result.JobId, result.FileId, result.Success, result.WasDryRun);

        return NoContent();
    }

    /// <summary>
    /// Get connected cleaner services status.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(IReadOnlyList<CleanerStatusDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<CleanerStatusDto>> GetCleanerStatus()
    {
        return Ok(CleanerHub.GetConnectedCleanerStatuses());
    }

    private async Task DeleteThumbnailAsync(string thumbnailPath, CancellationToken ct)
    {
        try
        {
            var thumbnailsBucket = _configuration["Minio:ThumbnailsBucket"] ?? "thumbnails";
            await _objectStorage.DeleteAsync(thumbnailsBucket, thumbnailPath, ct);
            _logger.LogInformation("Deleted thumbnail from MinIO: {Bucket}/{ThumbnailPath}", thumbnailsBucket, thumbnailPath);
        }
        catch (Exception ex)
        {
            // Log but don't fail - thumbnail cleanup is best effort
            _logger.LogWarning(ex, "Failed to delete thumbnail {ThumbnailPath}", thumbnailPath);
        }
    }

    private static CleanerJobDto MapToDto(CleanerJob job)
    {
        return new CleanerJobDto
        {
            Id = job.Id,
            Status = job.Status,
            Category = job.Category,
            DryRun = job.DryRun,
            TotalFiles = job.TotalFiles,
            ProcessedFiles = job.ProcessedFiles,
            SucceededFiles = job.SucceededFiles,
            FailedFiles = job.FailedFiles,
            SkippedFiles = job.SkippedFiles,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            ErrorMessage = job.ErrorMessage,
            Files = job.Files?.Select(f => new CleanerJobFileDto
            {
                Id = f.Id,
                FileId = f.FileId,
                FilePath = f.FilePath,
                FileHash = f.FileHash,
                FileSize = f.FileSize,
                Status = f.Status,
                ArchivePath = f.ArchivePath,
                ErrorMessage = f.ErrorMessage,
                ProcessedAt = f.ProcessedAt
            }).ToList()
        };
    }
}

public record ArchiveFileResult
{
    public bool Success { get; init; }
    public string? ArchivePath { get; init; }
    public string? Error { get; init; }
}
