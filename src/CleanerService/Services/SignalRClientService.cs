using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using Shared.Dtos;

namespace CleanerService.Services;

public interface ISignalRClientService : IAsyncDisposable
{
    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
    bool IsConnected { get; }
}

public class SignalRClientService : ISignalRClientService
{
    private readonly HubConnection _hubConnection;
    private readonly IDeleteService _deleteService;
    private readonly ICleanerStatusService _statusService;
    private readonly CleanerServiceOptions _options;
    private readonly ILogger<SignalRClientService> _logger;
    private readonly string _hostname;
    private Timer? _heartbeatTimer;

    public bool IsConnected => _hubConnection.State == HubConnectionState.Connected;

    public SignalRClientService(
        IDeleteService deleteService,
        ICleanerStatusService statusService,
        IOptions<CleanerServiceOptions> options,
        ILogger<SignalRClientService> logger)
    {
        _deleteService = deleteService;
        _statusService = statusService;
        _options = options.Value;
        _logger = logger;
        _hostname = Environment.MachineName;

        var baseUrl = _options.ApiBaseUrl.TrimEnd('/');
        var hubUrl = $"{baseUrl}/hubs/cleaner?cleanerId={_hostname}&hostname={_hostname}";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromMinutes(1)
            })
            .Build();

        // Register command handlers
        _hubConnection.On<DeleteFileRequest>("DeleteFile", HandleDeleteFileAsync);
        _hubConnection.On<IEnumerable<DeleteFileRequest>>("DeleteFiles", HandleDeleteFilesAsync);
        _hubConnection.On<Guid>("CancelJob", HandleCancelJobAsync);
        _hubConnection.On<bool>("SetDryRun", HandleSetDryRunAsync);
        _hubConnection.On("RequestStatus", HandleStatusRequestAsync);

        _hubConnection.Reconnecting += error =>
        {
            _logger.LogWarning(error, "SignalR reconnecting...");
            _statusService.SetState(CleanerState.Disconnected);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += connectionId =>
        {
            _logger.LogInformation("SignalR reconnected: {ConnectionId}", connectionId);
            _statusService.SetState(CleanerState.Idle);
            return Task.CompletedTask;
        };

        _hubConnection.Closed += error =>
        {
            _logger.LogWarning(error, "SignalR connection closed");
            _statusService.SetState(CleanerState.Disconnected);
            return Task.CompletedTask;
        };
    }

    public async Task StartAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _hubConnection.StartAsync(ct);
                _logger.LogInformation("SignalR connected to API hub from {Hostname}", _hostname);
                _statusService.SetState(CleanerState.Idle);

                // Start heartbeat timer to send status every 30 seconds
                _heartbeatTimer = new Timer(
                    async _ => await SendStatusAsync(),
                    null,
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(30));

                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to SignalR hub, retrying in 10s...");
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_heartbeatTimer != null)
        {
            await _heartbeatTimer.DisposeAsync();
            _heartbeatTimer = null;
        }

        if (_hubConnection.State != HubConnectionState.Disconnected)
        {
            await _hubConnection.StopAsync(ct);
        }
    }

    private async Task SendStatusAsync()
    {
        if (_hubConnection.State != HubConnectionState.Connected)
            return;

        try
        {
            var status = _statusService.GetStatus();
            await _hubConnection.InvokeAsync("ReportStatus", status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send status to hub");
        }
    }

    private async Task HandleStatusRequestAsync()
    {
        _logger.LogDebug("Received status request from hub");
        await SendStatusAsync();
    }

    private async Task HandleDeleteFileAsync(DeleteFileRequest request)
    {
        _logger.LogInformation("Received delete command: Job={JobId}, File={FileId}, Path={Path}",
            request.JobId, request.FileId, request.FilePath);

        try
        {
            await ReportProgressAsync(request.JobId, request.FileId, "starting");

            var result = await _deleteService.DeleteFileAsync(request, CancellationToken.None);

            await ReportDeleteCompleteAsync(result);

            _logger.LogInformation("Delete command completed: Job={JobId}, File={FileId}, Success={Success}",
                request.JobId, request.FileId, result.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete command failed: Job={JobId}, File={FileId}",
                request.JobId, request.FileId);

            await ReportDeleteCompleteAsync(new DeleteFileResult
            {
                JobId = request.JobId,
                FileId = request.FileId,
                Success = false,
                Error = ex.Message,
                WasDryRun = _options.DryRunEnabled
            });
        }
    }

    private async Task HandleDeleteFilesAsync(IEnumerable<DeleteFileRequest> requests)
    {
        var succeeded = 0;
        var failed = 0;
        var skipped = 0;
        Guid? jobId = null;

        foreach (var request in requests)
        {
            jobId = request.JobId;

            try
            {
                var result = await _deleteService.DeleteFileAsync(request, CancellationToken.None);
                await ReportDeleteCompleteAsync(result);

                if (result.Success)
                {
                    if (result.WasDryRun)
                        skipped++;
                    else
                        succeeded++;
                }
                else
                {
                    failed++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete failed for file {FileId}", request.FileId);
                failed++;
            }
        }

        if (jobId.HasValue)
        {
            await ReportJobCompleteAsync(jobId.Value, succeeded, failed, skipped);
        }
    }

    private Task HandleCancelJobAsync(Guid jobId)
    {
        _logger.LogInformation("Received cancel job command: {JobId}", jobId);
        // TODO: Implement cancellation token propagation
        return Task.CompletedTask;
    }

    private Task HandleSetDryRunAsync(bool enabled)
    {
        _logger.LogInformation("Received SetDryRun command: {Enabled}", enabled);
        // Note: DryRun is currently configured via environment variable, not runtime
        return Task.CompletedTask;
    }

    private async Task ReportProgressAsync(Guid jobId, Guid fileId, string status)
    {
        try
        {
            await _hubConnection.InvokeAsync("ReportDeleteProgress", jobId, fileId, status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report progress for {FileId}", fileId);
        }
    }

    private async Task ReportDeleteCompleteAsync(DeleteFileResult result)
    {
        try
        {
            await _hubConnection.InvokeAsync("ReportDeleteComplete", result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report delete complete for {FileId}", result.FileId);
        }
    }

    private async Task ReportJobCompleteAsync(Guid jobId, int succeeded, int failed, int skipped)
    {
        try
        {
            await _hubConnection.InvokeAsync("ReportJobComplete", jobId, succeeded, failed, skipped);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report job complete for {JobId}", jobId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_heartbeatTimer != null)
        {
            await _heartbeatTimer.DisposeAsync();
        }
        await _hubConnection.DisposeAsync();
    }
}
