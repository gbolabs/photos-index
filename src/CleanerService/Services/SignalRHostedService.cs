namespace CleanerService.Services;

public class SignalRHostedService : BackgroundService
{
    private readonly ISignalRClientService _signalRClient;
    private readonly ILogger<SignalRHostedService> _logger;

    public SignalRHostedService(
        ISignalRClientService signalRClient,
        ILogger<SignalRHostedService> logger)
    {
        _signalRClient = signalRClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting SignalR client service...");
        await _signalRClient.StartAsync(stoppingToken);

        // Keep alive until shutdown
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping SignalR client service...");
        await _signalRClient.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
