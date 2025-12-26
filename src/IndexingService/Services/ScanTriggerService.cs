using System.Threading.Channels;

namespace IndexingService.Services;

public interface IScanTriggerService
{
    /// <summary>
    /// Request an immediate scan. Optionally specify a directory ID to scan only that directory.
    /// </summary>
    void TriggerScan(Guid? directoryId = null);

    /// <summary>
    /// Wait for the next scan trigger.
    /// </summary>
    ValueTask<ScanTrigger> WaitForTriggerAsync(CancellationToken cancellationToken);
}

public record ScanTrigger(Guid? DirectoryId, DateTime RequestedAt);

public class ScanTriggerService : IScanTriggerService
{
    private readonly Channel<ScanTrigger> _channel;
    private readonly ILogger<ScanTriggerService> _logger;

    public ScanTriggerService(ILogger<ScanTriggerService> logger)
    {
        _logger = logger;
        // Bounded channel with capacity 1 - we only need to track that a scan was requested
        // Additional requests while a scan is pending are coalesced
        _channel = Channel.CreateBounded<ScanTrigger>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    public void TriggerScan(Guid? directoryId = null)
    {
        var trigger = new ScanTrigger(directoryId, DateTime.UtcNow);
        if (_channel.Writer.TryWrite(trigger))
        {
            _logger.LogInformation("Scan triggered: DirectoryId={DirectoryId}", directoryId?.ToString() ?? "all");
        }
        else
        {
            _logger.LogDebug("Scan trigger coalesced (previous trigger still pending)");
        }
    }

    public ValueTask<ScanTrigger> WaitForTriggerAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}
