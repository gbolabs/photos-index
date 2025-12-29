using System.Threading.Channels;
using Api.Hubs;
using Database;
using Database.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Shared.Dtos;

namespace Api.Services;

public interface ICleanerJobService
{
    Task QueueJobAsync(Guid jobId, CancellationToken ct = default);
}

/// <summary>
/// Service that manages cleaner job processing and dispatches work to connected CleanerService instances.
/// </summary>
public class CleanerJobService : ICleanerJobService
{
    private readonly Channel<Guid> _jobQueue = Channel.CreateUnbounded<Guid>();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<CleanerHub, ICleanerClient> _hubContext;
    private readonly ILogger<CleanerJobService> _logger;

    public CleanerJobService(
        IServiceScopeFactory scopeFactory,
        IHubContext<CleanerHub, ICleanerClient> hubContext,
        ILogger<CleanerJobService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task QueueJobAsync(Guid jobId, CancellationToken ct = default)
    {
        await _jobQueue.Writer.WriteAsync(jobId, ct);
        _logger.LogInformation("Queued cleaner job {JobId} for processing", jobId);
    }

    public ChannelReader<Guid> GetJobQueue() => _jobQueue.Reader;

    public async Task ProcessJobAsync(Guid jobId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotosDbContext>();

        var job = await db.CleanerJobs
            .Include(j => j.Files)
            .FirstOrDefaultAsync(j => j.Id == jobId, ct);

        if (job == null)
        {
            _logger.LogWarning("Cleaner job {JobId} not found", jobId);
            return;
        }

        if (job.Status == CleanerJobStatus.Cancelled)
        {
            _logger.LogInformation("Cleaner job {JobId} was cancelled", jobId);
            return;
        }

        if (!CleanerHub.HasConnectedCleaner())
        {
            _logger.LogWarning("No cleaner service connected, cannot process job {JobId}", jobId);
            job.Status = CleanerJobStatus.Failed;
            job.ErrorMessage = "No cleaner service connected";
            await db.SaveChangesAsync(ct);
            return;
        }

        job.Status = CleanerJobStatus.InProgress;
        job.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Starting cleaner job {JobId} with {FileCount} files", jobId, job.Files.Count);

        // Send delete requests to connected cleaner services
        var pendingFiles = job.Files.Where(f => f.Status == CleanerFileStatus.Pending).ToList();

        foreach (var file in pendingFiles)
        {
            if (ct.IsCancellationRequested)
            {
                _logger.LogInformation("Job {JobId} cancelled", jobId);
                break;
            }

            var request = new DeleteFileRequest
            {
                JobId = jobId,
                FileId = file.FileId,
                FilePath = file.FilePath,
                FileHash = file.FileHash,
                FileSize = file.FileSize,
                Category = job.Category
            };

            try
            {
                await _hubContext.Clients.All.DeleteFile(request);
                _logger.LogDebug("Sent delete request for file {FileId} in job {JobId}", file.FileId, jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send delete request for file {FileId}", file.FileId);
            }
        }
    }
}
