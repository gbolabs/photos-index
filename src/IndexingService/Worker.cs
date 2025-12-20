using IndexingService.Services;

namespace IndexingService;

public class Worker : BackgroundService
{
    private readonly IIndexingOrchestrator _orchestrator;
    private readonly ILogger<Worker> _logger;
    private readonly TimeSpan _indexingInterval;

    public Worker(
        IIndexingOrchestrator orchestrator,
        ILogger<Worker> logger,
        IConfiguration configuration)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var intervalMinutes = configuration.GetValue<int?>("IndexingIntervalMinutes") ?? 5;
        _indexingInterval = TimeSpan.FromMinutes(intervalMinutes);

        _logger.LogInformation("Indexing worker configured with interval: {Interval}", _indexingInterval);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Indexing worker started");

        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting indexing cycle");
                var jobs = await _orchestrator.RunIndexingCycleAsync(stoppingToken);

                var totalScanned = jobs.Sum(j => j.FilesScanned);
                var totalIngested = jobs.Sum(j => j.FilesIngested);
                var totalFailed = jobs.Sum(j => j.FilesFailed);

                _logger.LogInformation(
                    "Indexing cycle completed: {Directories} directories, {Scanned} files scanned, {Ingested} ingested, {Failed} failed",
                    jobs.Count,
                    totalScanned,
                    totalIngested,
                    totalFailed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during indexing cycle");
            }

            _logger.LogInformation("Next indexing cycle in {Interval}", _indexingInterval);
            await Task.Delay(_indexingInterval, stoppingToken);
        }

        _logger.LogInformation("Indexing worker stopped");
    }
}
