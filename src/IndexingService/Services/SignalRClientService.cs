using IndexingService.ApiClient;
using IndexingService.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace IndexingService.Services;

public interface ISignalRClientService : IAsyncDisposable
{
    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
    bool IsConnected { get; }
}

public class SignalRClientService : ISignalRClientService
{
    private readonly HubConnection _hubConnection;
    private readonly IPhotosApiClient _apiClient;
    private readonly ILogger<SignalRClientService> _logger;
    private readonly string _hostname;

    public bool IsConnected => _hubConnection.State == HubConnectionState.Connected;

    public SignalRClientService(
        IPhotosApiClient apiClient,
        IOptions<IndexingOptions> options,
        ILogger<SignalRClientService> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
        _hostname = Environment.MachineName;

        var baseUrl = options.Value.ApiBaseUrl.TrimEnd('/');
        var hubUrl = $"{baseUrl}/hubs/indexer?indexerId={_hostname}&hostname={_hostname}";

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
        _hubConnection.On<Guid, string>("ReprocessFile", HandleReprocessFileAsync);
        _hubConnection.On<IEnumerable<ReprocessRequest>>("ReprocessFiles", HandleReprocessFilesAsync);

        _hubConnection.Reconnecting += error =>
        {
            _logger.LogWarning(error, "SignalR reconnecting...");
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += connectionId =>
        {
            _logger.LogInformation("SignalR reconnected: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };

        _hubConnection.Closed += error =>
        {
            _logger.LogWarning(error, "SignalR connection closed");
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
        if (_hubConnection.State != HubConnectionState.Disconnected)
        {
            await _hubConnection.StopAsync(ct);
        }
    }

    private async Task HandleReprocessFileAsync(Guid fileId, string filePath)
    {
        _logger.LogInformation("Received reprocess command: {FileId} -> {Path}", fileId, filePath);

        try
        {
            await ReportProgressAsync(fileId, "checking");

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File not found for reprocess: {Path}", filePath);
                await ReportCompleteAsync(fileId, false, "File not found on disk");
                return;
            }

            await ReportProgressAsync(fileId, "reading");

            var fileInfo = new FileInfo(filePath);
            await using var stream = File.OpenRead(filePath);

            await ReportProgressAsync(fileId, "hashing");

            // Compute hash for the file
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = await sha256.ComputeHashAsync(stream, CancellationToken.None);
            var fileHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

            // Reset stream for upload
            stream.Position = 0;

            await ReportProgressAsync(fileId, "uploading");

            // Re-ingest through normal API flow
            await _apiClient.IngestFileWithContentAsync(
                new FileIngestRequest
                {
                    ScanDirectoryId = Guid.Empty, // Will be resolved by API
                    FilePath = filePath,
                    FileName = fileInfo.Name,
                    FileHash = fileHash,
                    FileSize = fileInfo.Length,
                    ModifiedAt = fileInfo.LastWriteTimeUtc
                },
                stream,
                GetContentType(filePath),
                CancellationToken.None);

            await ReportCompleteAsync(fileId, true, null);
            _logger.LogInformation("Reprocess complete: {FileId}", fileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reprocess failed: {FileId}", fileId);
            await ReportCompleteAsync(fileId, false, ex.Message);
        }
    }

    private async Task HandleReprocessFilesAsync(IEnumerable<ReprocessRequest> files)
    {
        foreach (var file in files)
        {
            await HandleReprocessFileAsync(file.FileId, file.FilePath);
        }
    }

    private async Task ReportProgressAsync(Guid fileId, string status)
    {
        try
        {
            await _hubConnection.InvokeAsync("ReportProgress", fileId, status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report progress for {FileId}", fileId);
        }
    }

    private async Task ReportCompleteAsync(Guid fileId, bool success, string? error)
    {
        try
        {
            await _hubConnection.InvokeAsync("ReportComplete", fileId, success, error);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report completion for {FileId}", fileId);
        }
    }

    private static string GetContentType(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".heic" => "image/heic",
            ".heif" => "image/heif",
            ".webp" => "image/webp",
            ".avif" => "image/avif",
            ".bmp" => "image/bmp",
            ".tiff" or ".tif" => "image/tiff",
            _ => "application/octet-stream"
        };
    }

    public async ValueTask DisposeAsync()
    {
        await _hubConnection.DisposeAsync();
    }
}

public record ReprocessRequest(Guid FileId, string FilePath);
