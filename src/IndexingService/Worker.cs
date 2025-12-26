using IndexingService.Models;
using IndexingService.Services;
using Shared.Dtos;

namespace IndexingService;

public class Worker : BackgroundService
{
    private readonly IIndexingOrchestrator _orchestrator;
    private readonly IScanTriggerService _scanTrigger;
    private readonly IIndexerStatusService _statusService;
    private readonly ILogger<Worker> _logger;
    private readonly TimeSpan _indexingInterval;

    public Worker(
        IIndexingOrchestrator orchestrator,
        IScanTriggerService scanTrigger,
        IIndexerStatusService statusService,
        ILogger<Worker> logger,
        IConfiguration configuration)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _scanTrigger = scanTrigger ?? throw new ArgumentNullException(nameof(scanTrigger));
        _statusService = statusService ?? throw new ArgumentNullException(nameof(statusService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var intervalMinutes = configuration.GetValue<int?>("IndexingIntervalMinutes") ?? 5;
        _indexingInterval = TimeSpan.FromMinutes(intervalMinutes);

        _logger.LogInformation("Indexing worker configured with interval: {Interval}", _indexingInterval);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Indexing worker started");

        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        // Start background task to listen for manual triggers
        _ = ListenForTriggersAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunIndexingCycleAsync(stoppingToken);

            _logger.LogInformation("Next indexing cycle in {Interval}", _indexingInterval);
            await Task.Delay(_indexingInterval, stoppingToken);
        }

        _logger.LogInformation("Indexing worker stopped");
    }

    private async Task ListenForTriggersAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Listening for manual scan triggers");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var trigger = await _scanTrigger.WaitForTriggerAsync(stoppingToken);
                _logger.LogInformation("Received manual scan trigger: DirectoryId={DirectoryId}",
                    trigger.DirectoryId?.ToString() ?? "all");

                await RunIndexingCycleAsync(stoppingToken, trigger.DirectoryId);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing scan trigger");
            }
        }
    }

    private async Task RunIndexingCycleAsync(CancellationToken stoppingToken, Guid? directoryId = null)
    {
        try
        {
            _logger.LogInformation("Starting indexing cycle");
            _statusService.SetState(IndexerState.Scanning);
            _statusService.ScanStarted();

            var jobs = directoryId.HasValue
                ? await RunSingleDirectoryAsync(directoryId.Value, stoppingToken)
                : await _orchestrator.RunIndexingCycleAsync(stoppingToken);

            var totalScanned = jobs.Sum(j => j.FilesScanned);
            var totalIngested = jobs.Sum(j => j.FilesIngested);
            var totalFailed = jobs.Sum(j => j.FilesFailed);

            _logger.LogInformation(
                "Indexing cycle completed: {Directories} directories, {Scanned} files scanned, {Ingested} ingested, {Failed} failed",
                jobs.Count,
                totalScanned,
                totalIngested,
                totalFailed);

            _statusService.ScanCompleted();
            _statusService.SetState(IndexerState.Idle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during indexing cycle");
            _statusService.RecordError(ex.Message);
            _statusService.SetState(IndexerState.Error);
        }
    }

    private async Task<IReadOnlyList<IndexingJob>> RunSingleDirectoryAsync(Guid directoryId, CancellationToken stoppingToken)
    {
        // For now, run the full cycle - single directory support can be added later
        // This is a simplification; a full implementation would fetch the directory path from the API
        _logger.LogInformation("Single directory scan requested for {DirectoryId}, running full cycle", directoryId);
        return await _orchestrator.RunIndexingCycleAsync(stoppingToken);
    }
}
