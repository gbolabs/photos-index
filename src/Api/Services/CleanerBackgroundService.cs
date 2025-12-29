namespace Api.Services;

/// <summary>
/// Background service that processes queued cleaner jobs.
/// </summary>
public class CleanerBackgroundService : BackgroundService
{
    private readonly CleanerJobService _jobService;
    private readonly ILogger<CleanerBackgroundService> _logger;

    public CleanerBackgroundService(
        CleanerJobService jobService,
        ILogger<CleanerBackgroundService> logger)
    {
        _jobService = jobService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cleaner background service started");

        await foreach (var jobId in _jobService.GetJobQueue().ReadAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Processing cleaner job {JobId}", jobId);
                await _jobService.ProcessJobAsync(jobId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing cleaner job {JobId}", jobId);
            }
        }

        _logger.LogInformation("Cleaner background service stopped");
    }
}
