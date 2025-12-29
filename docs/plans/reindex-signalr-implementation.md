# Implementation Plan: Re-index Files via SignalR

## Overview

Enable re-processing of failed files (missing metadata/thumbnails) through a SignalR connection between API and Indexer. The API sends commands to connected Indexer(s), which re-read files from disk and re-ingest them through the normal pipeline.

## Architecture

```
┌─────────┐                    ┌─────────┐                    ┌──────────────┐
│   UI    │───HTTP POST───────▶│   API   │───RabbitMQ───────▶│ MetadataSvc  │
│(Angular)│◀──SignalR──────────│ (Hub)   │                    │ ThumbnailSvc │
└─────────┘                    └────┬────┘                    └──────────────┘
                                    │
                               SignalR
                                    │
                               ┌────▼────┐
                               │ Indexer │
                               │(Synology)│
                               └─────────┘
```

**Key principle:** Indexer remains a simple client (HTTP + SignalR). No RabbitMQ consumption.

---

## Database Schema Updates

### Existing Fields (already in IndexedFile)
- `IndexedAt` - when file was first indexed
- `LastError` - last processing error message
- `RetryCount` - number of reprocess attempts

### New Fields to Add

```csharp
// src/Database/Entities/IndexedFile.cs
public DateTime? MetadataProcessedAt { get; set; }   // When EXIF extracted
public DateTime? ThumbnailProcessedAt { get; set; }  // When thumbnail generated
```

### Migration

```bash
dotnet ef migrations add AddProcessingTimestamps --project src/Database --startup-project src/Api
```

### Query Filters

```sql
-- Files never processed (indexed but processing failed/pending)
SELECT * FROM IndexedFiles WHERE MetadataProcessedAt IS NULL;

-- Files missing thumbnails
SELECT * FROM IndexedFiles WHERE ThumbnailProcessedAt IS NULL;

-- Recently indexed but not processed (stuck in queue?)
SELECT * FROM IndexedFiles
WHERE IndexedAt > NOW() - INTERVAL '1 hour'
  AND MetadataProcessedAt IS NULL;

-- HEIC files to reprocess after v0.3.10
SELECT * FROM IndexedFiles
WHERE FileName ILIKE '%.heic'
  AND MetadataProcessedAt IS NULL;
```

---

## Implementation Phases

### Phase 1: Database + API SignalR Hub

#### 1.1 Add Database Fields
**File:** `src/Database/Entities/IndexedFile.cs`

```csharp
// Add after RetryCount
public DateTime? MetadataProcessedAt { get; set; }
public DateTime? ThumbnailProcessedAt { get; set; }
```

#### 1.2 Update Message Consumers to Set Timestamps
**File:** `src/Api/Consumers/MetadataExtractedConsumer.cs`

```csharp
// After updating metadata fields
file.MetadataProcessedAt = DateTime.UtcNow;
if (!message.Success)
{
    file.LastError = message.ErrorMessage;
    file.RetryCount++;
}
```

**File:** `src/Api/Consumers/ThumbnailGeneratedConsumer.cs`

```csharp
file.ThumbnailProcessedAt = DateTime.UtcNow;
if (!message.Success)
{
    file.LastError = message.ErrorMessage;
    file.RetryCount++;
}
```

#### 1.3 Create SignalR Hub
**File:** `src/Api/Hubs/IndexerHub.cs`

```csharp
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

public interface IIndexerClient
{
    Task ReprocessFile(Guid fileId, string filePath);
    Task ReprocessFiles(IEnumerable<ReprocessFileRequest> files);
}

public interface IUIClient
{
    Task ReprocessProgress(Guid fileId, string status);
    Task ReprocessComplete(Guid fileId, bool success, string? error);
    Task IndexerConnected(string indexerId, string hostname);
    Task IndexerDisconnected(string indexerId);
}

public class IndexerHub : Hub<IIndexerClient>
{
    private readonly ILogger<IndexerHub> _logger;
    private static readonly Dictionary<string, IndexerConnection> _connectedIndexers = new();

    public IndexerHub(ILogger<IndexerHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var indexerId = Context.GetHttpContext()?.Request.Query["indexerId"].ToString()
            ?? Context.ConnectionId;
        var hostname = Context.GetHttpContext()?.Request.Query["hostname"].ToString()
            ?? "unknown";

        _connectedIndexers[Context.ConnectionId] = new IndexerConnection(indexerId, hostname);
        _logger.LogInformation("Indexer connected: {IndexerId} from {Hostname}", indexerId, hostname);

        // Notify UI clients
        await Clients.Group("ui").SendAsync("IndexerConnected", indexerId, hostname);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connectedIndexers.TryGetValue(Context.ConnectionId, out var connection))
        {
            _connectedIndexers.Remove(Context.ConnectionId);
            _logger.LogInformation("Indexer disconnected: {IndexerId}", connection.IndexerId);
            await Clients.Group("ui").SendAsync("IndexerDisconnected", connection.IndexerId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    // Called by Indexer to report progress
    public async Task ReportProgress(Guid fileId, string status)
    {
        await Clients.Group("ui").SendAsync("ReprocessProgress", fileId, status);
    }

    // Called by Indexer when done
    public async Task ReportComplete(Guid fileId, bool success, string? error)
    {
        await Clients.Group("ui").SendAsync("ReprocessComplete", fileId, success, error);
    }

    // UI clients join this group
    public async Task JoinUIGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "ui");
    }

    public static IReadOnlyCollection<IndexerConnection> GetConnectedIndexers()
        => _connectedIndexers.Values.ToList();
}

public record IndexerConnection(string IndexerId, string Hostname);
public record ReprocessFileRequest(Guid FileId, string FilePath);
```

#### 1.4 Create Reprocess Service
**File:** `src/Api/Services/ReprocessService.cs`

```csharp
using Api.Hubs;
using Database;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public interface IReprocessService
{
    Task<ReprocessResult> ReprocessFileAsync(Guid fileId, CancellationToken ct);
    Task<ReprocessResult> ReprocessFilesAsync(IEnumerable<Guid> fileIds, CancellationToken ct);
    Task<ReprocessResult> ReprocessByFilterAsync(ReprocessFilter filter, int? limit, CancellationToken ct);
    IReadOnlyCollection<IndexerConnection> GetConnectedIndexers();
}

public class ReprocessService : IReprocessService
{
    private readonly IHubContext<IndexerHub, IIndexerClient> _hubContext;
    private readonly PhotosDbContext _dbContext;
    private readonly ILogger<ReprocessService> _logger;

    public ReprocessService(
        IHubContext<IndexerHub, IIndexerClient> hubContext,
        PhotosDbContext dbContext,
        ILogger<ReprocessService> logger)
    {
        _hubContext = hubContext;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ReprocessResult> ReprocessFileAsync(Guid fileId, CancellationToken ct)
    {
        var file = await _dbContext.IndexedFiles.FindAsync(fileId, ct);
        if (file is null)
            return ReprocessResult.NotFound(fileId);

        if (!IndexerHub.GetConnectedIndexers().Any())
            return ReprocessResult.NoIndexerConnected();

        // Reset processing state
        file.MetadataProcessedAt = null;
        file.ThumbnailProcessedAt = null;
        file.LastError = null;
        await _dbContext.SaveChangesAsync(ct);

        await _hubContext.Clients.All.ReprocessFile(fileId, file.FilePath);
        _logger.LogInformation("Sent reprocess command for file {FileId}: {Path}", fileId, file.FilePath);

        return ReprocessResult.Queued(1);
    }

    public async Task<ReprocessResult> ReprocessFilesAsync(IEnumerable<Guid> fileIds, CancellationToken ct)
    {
        var files = await _dbContext.IndexedFiles
            .Where(f => fileIds.Contains(f.Id))
            .ToListAsync(ct);

        if (!files.Any())
            return ReprocessResult.NotFound(Guid.Empty);

        if (!IndexerHub.GetConnectedIndexers().Any())
            return ReprocessResult.NoIndexerConnected();

        // Reset processing state
        foreach (var file in files)
        {
            file.MetadataProcessedAt = null;
            file.ThumbnailProcessedAt = null;
            file.LastError = null;
        }
        await _dbContext.SaveChangesAsync(ct);

        var requests = files.Select(f => new ReprocessFileRequest(f.Id, f.FilePath));
        await _hubContext.Clients.All.ReprocessFiles(requests);

        return ReprocessResult.Queued(files.Count);
    }

    public async Task<ReprocessResult> ReprocessByFilterAsync(ReprocessFilter filter, int? limit, CancellationToken ct)
    {
        var query = _dbContext.IndexedFiles.AsQueryable();

        query = filter switch
        {
            ReprocessFilter.MissingMetadata => query.Where(f => f.MetadataProcessedAt == null),
            ReprocessFilter.MissingThumbnail => query.Where(f => f.ThumbnailProcessedAt == null),
            ReprocessFilter.Failed => query.Where(f => f.LastError != null),
            ReprocessFilter.Heic => query.Where(f =>
                f.FileName.ToLower().EndsWith(".heic") && f.MetadataProcessedAt == null),
            _ => throw new ArgumentException($"Unknown filter: {filter}")
        };

        var files = await query
            .OrderBy(f => f.IndexedAt)
            .Take(limit ?? 1000)
            .ToListAsync(ct);

        if (!files.Any())
            return ReprocessResult.Queued(0);

        if (!IndexerHub.GetConnectedIndexers().Any())
            return ReprocessResult.NoIndexerConnected();

        // Reset processing state
        foreach (var file in files)
        {
            file.MetadataProcessedAt = null;
            file.ThumbnailProcessedAt = null;
            file.LastError = null;
        }
        await _dbContext.SaveChangesAsync(ct);

        var requests = files.Select(f => new ReprocessFileRequest(f.Id, f.FilePath));
        await _hubContext.Clients.All.ReprocessFiles(requests);

        _logger.LogInformation("Sent reprocess command for {Count} files with filter {Filter}",
            files.Count, filter);

        return ReprocessResult.Queued(files.Count);
    }

    public IReadOnlyCollection<IndexerConnection> GetConnectedIndexers()
        => IndexerHub.GetConnectedIndexers();
}

public enum ReprocessFilter
{
    MissingMetadata,
    MissingThumbnail,
    Failed,
    Heic
}

public record ReprocessResult(bool Success, int QueuedCount, string? Error)
{
    public static ReprocessResult Queued(int count) => new(true, count, null);
    public static ReprocessResult NotFound(Guid fileId) => new(false, 0, $"File not found: {fileId}");
    public static ReprocessResult NoIndexerConnected() => new(false, 0, "No indexer connected");
}
```

#### 1.5 Create Reprocess Controller
**File:** `src/Api/Controllers/ReprocessController.cs`

```csharp
using Api.Hubs;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReprocessController : ControllerBase
{
    private readonly IReprocessService _reprocessService;

    public ReprocessController(IReprocessService reprocessService)
    {
        _reprocessService = reprocessService;
    }

    /// <summary>
    /// Reprocess a single file by ID
    /// </summary>
    [HttpPost("file/{fileId:guid}")]
    public async Task<ActionResult<ReprocessResult>> ReprocessFile(Guid fileId, CancellationToken ct)
    {
        var result = await _reprocessService.ReprocessFileAsync(fileId, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Reprocess multiple files by ID
    /// </summary>
    [HttpPost("files")]
    public async Task<ActionResult<ReprocessResult>> ReprocessFiles(
        [FromBody] ReprocessFilesRequest request,
        CancellationToken ct)
    {
        var result = await _reprocessService.ReprocessFilesAsync(request.FileIds, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Reprocess files by filter (MissingMetadata, MissingThumbnail, Failed, Heic)
    /// </summary>
    [HttpPost("filter/{filter}")]
    public async Task<ActionResult<ReprocessResult>> ReprocessByFilter(
        ReprocessFilter filter,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        var result = await _reprocessService.ReprocessByFilterAsync(filter, limit, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Get list of connected indexers
    /// </summary>
    [HttpGet("indexers")]
    public ActionResult<IEnumerable<IndexerConnection>> GetConnectedIndexers()
    {
        return Ok(_reprocessService.GetConnectedIndexers());
    }

    /// <summary>
    /// Get count of files needing reprocessing by filter
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<ReprocessStats>> GetStats(
        [FromServices] Database.PhotosDbContext db,
        CancellationToken ct)
    {
        var stats = new ReprocessStats
        {
            MissingMetadata = await db.IndexedFiles.CountAsync(f => f.MetadataProcessedAt == null, ct),
            MissingThumbnail = await db.IndexedFiles.CountAsync(f => f.ThumbnailProcessedAt == null, ct),
            Failed = await db.IndexedFiles.CountAsync(f => f.LastError != null, ct),
            HeicUnprocessed = await db.IndexedFiles.CountAsync(f =>
                f.FileName.ToLower().EndsWith(".heic") && f.MetadataProcessedAt == null, ct),
            ConnectedIndexers = _reprocessService.GetConnectedIndexers().Count
        };
        return Ok(stats);
    }
}

public record ReprocessFilesRequest(IEnumerable<Guid> FileIds);

public record ReprocessStats
{
    public int MissingMetadata { get; init; }
    public int MissingThumbnail { get; init; }
    public int Failed { get; init; }
    public int HeicUnprocessed { get; init; }
    public int ConnectedIndexers { get; init; }
}
```

#### 1.6 Register Services in Program.cs
**File:** `src/Api/Program.cs` (modify)

```csharp
// Add SignalR
builder.Services.AddSignalR();

// Add reprocess service
builder.Services.AddScoped<IReprocessService, ReprocessService>();

// ... after app.MapControllers() ...

// Map SignalR hub
app.MapHub<IndexerHub>("/hubs/indexer");
```

---

### Phase 2: Indexer SignalR Client

#### 2.1 Add NuGet Package
```bash
dotnet add src/IndexingService package Microsoft.AspNetCore.SignalR.Client
```

#### 2.2 Create SignalR Client Service
**File:** `src/IndexingService/Services/SignalRClientService.cs`

```csharp
using IndexingService.ApiClient;
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
    private readonly IApiClient _apiClient;
    private readonly ILogger<SignalRClientService> _logger;
    private readonly string _hostname;

    public bool IsConnected => _hubConnection.State == HubConnectionState.Connected;

    public SignalRClientService(
        IApiClient apiClient,
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
            .WithAutomaticReconnect(new[] {
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

            await ReportProgressAsync(fileId, "uploading");

            // Re-ingest through normal API flow
            await _apiClient.IngestFileWithContentAsync(
                new FileIngestRequest
                {
                    FilePath = filePath,
                    FileName = fileInfo.Name,
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
            _ => "application/octet-stream"
        };
    }

    public async ValueTask DisposeAsync()
    {
        await _hubConnection.DisposeAsync();
    }
}

public record ReprocessRequest(Guid FileId, string FilePath);
```

#### 2.3 Create Hosted Service
**File:** `src/IndexingService/Services/SignalRHostedService.cs`

```csharp
namespace IndexingService.Services;

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
```

#### 2.4 Register in Program.cs
**File:** `src/IndexingService/Program.cs` (modify)

```csharp
// Add SignalR client services
builder.Services.AddSingleton<ISignalRClientService, SignalRClientService>();
builder.Services.AddHostedService<SignalRHostedService>();
```

---

### Phase 3: Angular UI

#### 3.1 Install SignalR Package
```bash
cd src/Web && npm install @microsoft/signalr
```

#### 3.2 Create Reprocess Service
**File:** `src/Web/src/app/services/reprocess.service.ts`

```typescript
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { BehaviorSubject, Observable, firstValueFrom } from 'rxjs';

export interface ReprocessResult {
  success: boolean;
  queuedCount: number;
  error?: string;
}

export interface ReprocessStats {
  missingMetadata: number;
  missingThumbnail: number;
  failed: number;
  heicUnprocessed: number;
  connectedIndexers: number;
}

export interface ReprocessProgress {
  fileId: string;
  status: 'checking' | 'reading' | 'uploading' | 'complete' | 'failed';
  error?: string;
}

export interface IndexerConnection {
  indexerId: string;
  hostname: string;
}

@Injectable({ providedIn: 'root' })
export class ReprocessService {
  private http = inject(HttpClient);
  private hubConnection?: HubConnection;

  private progressSubject = new BehaviorSubject<Map<string, ReprocessProgress>>(new Map());
  private indexersSubject = new BehaviorSubject<IndexerConnection[]>([]);
  private connectedSubject = new BehaviorSubject<boolean>(false);

  progress$ = this.progressSubject.asObservable();
  indexers$ = this.indexersSubject.asObservable();
  connected$ = this.connectedSubject.asObservable();

  async connect(): Promise<void> {
    if (this.hubConnection?.state === HubConnectionState.Connected) return;

    this.hubConnection = new HubConnectionBuilder()
      .withUrl('/hubs/indexer')
      .withAutomaticReconnect()
      .build();

    this.hubConnection.on('ReprocessProgress', (fileId: string, status: string) => {
      const progress = this.progressSubject.value;
      progress.set(fileId, { fileId, status: status as any });
      this.progressSubject.next(new Map(progress));
    });

    this.hubConnection.on('ReprocessComplete', (fileId: string, success: boolean, error?: string) => {
      const progress = this.progressSubject.value;
      progress.set(fileId, {
        fileId,
        status: success ? 'complete' : 'failed',
        error
      });
      this.progressSubject.next(new Map(progress));
    });

    this.hubConnection.on('IndexerConnected', (indexerId: string, hostname: string) => {
      const indexers = [...this.indexersSubject.value, { indexerId, hostname }];
      this.indexersSubject.next(indexers);
    });

    this.hubConnection.on('IndexerDisconnected', (indexerId: string) => {
      const indexers = this.indexersSubject.value.filter(i => i.indexerId !== indexerId);
      this.indexersSubject.next(indexers);
    });

    this.hubConnection.onreconnected(() => this.connectedSubject.next(true));
    this.hubConnection.onclose(() => this.connectedSubject.next(false));

    await this.hubConnection.start();
    await this.hubConnection.invoke('JoinUIGroup');
    this.connectedSubject.next(true);

    // Load initial indexers
    const indexers = await firstValueFrom(this.getConnectedIndexers());
    this.indexersSubject.next(indexers);
  }

  disconnect(): void {
    this.hubConnection?.stop();
  }

  // API calls
  getStats(): Observable<ReprocessStats> {
    return this.http.get<ReprocessStats>('/api/reprocess/stats');
  }

  getConnectedIndexers(): Observable<IndexerConnection[]> {
    return this.http.get<IndexerConnection[]>('/api/reprocess/indexers');
  }

  reprocessFile(fileId: string): Observable<ReprocessResult> {
    return this.http.post<ReprocessResult>(`/api/reprocess/file/${fileId}`, {});
  }

  reprocessFiles(fileIds: string[]): Observable<ReprocessResult> {
    return this.http.post<ReprocessResult>('/api/reprocess/files', { fileIds });
  }

  reprocessByFilter(
    filter: 'MissingMetadata' | 'MissingThumbnail' | 'Failed' | 'Heic',
    limit?: number
  ): Observable<ReprocessResult> {
    const params = limit ? `?limit=${limit}` : '';
    return this.http.post<ReprocessResult>(`/api/reprocess/filter/${filter}${params}`, {});
  }

  clearProgress(fileId?: string): void {
    if (fileId) {
      const progress = this.progressSubject.value;
      progress.delete(fileId);
      this.progressSubject.next(new Map(progress));
    } else {
      this.progressSubject.next(new Map());
    }
  }
}
```

#### 3.3 Create Admin Component
**File:** `src/Web/src/app/components/admin/admin.component.ts`

```typescript
import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatIconModule } from '@angular/material/icon';
import { ReprocessService, ReprocessStats } from '../../services/reprocess.service';
import { firstValueFrom } from 'rxjs';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatChipsModule,
    MatProgressBarModule,
    MatSnackBarModule,
    MatIconModule
  ],
  template: `
    <div class="admin-container">
      <mat-card>
        <mat-card-header>
          <mat-card-title>Reprocess Files</mat-card-title>
          <mat-card-subtitle>Re-trigger processing for failed or missing files</mat-card-subtitle>
        </mat-card-header>

        <mat-card-content>
          <!-- Indexer Status -->
          <section class="indexer-status">
            <h3>Connected Indexers</h3>
            <div class="indexers">
              @for (indexer of indexers$ | async; track indexer.indexerId) {
                <mat-chip color="primary" highlighted>
                  <mat-icon>dns</mat-icon>
                  {{ indexer.hostname }}
                </mat-chip>
              } @empty {
                <p class="warning">
                  <mat-icon>warning</mat-icon>
                  No indexer connected. Start the indexer service to enable reprocessing.
                </p>
              }
            </div>
          </section>

          <!-- Stats -->
          @if (stats) {
            <section class="stats">
              <h3>Files Needing Reprocessing</h3>
              <div class="stat-grid">
                <div class="stat-item">
                  <span class="stat-value">{{ stats.missingMetadata }}</span>
                  <span class="stat-label">Missing Metadata</span>
                </div>
                <div class="stat-item">
                  <span class="stat-value">{{ stats.missingThumbnail }}</span>
                  <span class="stat-label">Missing Thumbnail</span>
                </div>
                <div class="stat-item">
                  <span class="stat-value">{{ stats.heicUnprocessed }}</span>
                  <span class="stat-label">HEIC Unprocessed</span>
                </div>
                <div class="stat-item">
                  <span class="stat-value">{{ stats.failed }}</span>
                  <span class="stat-label">Failed</span>
                </div>
              </div>
            </section>
          }

          <!-- Actions -->
          <section class="actions">
            <h3>Bulk Actions</h3>
            <div class="action-buttons">
              <button mat-raised-button
                      [disabled]="!hasIndexer || processing"
                      (click)="reprocess('MissingMetadata')">
                Reprocess Missing Metadata
              </button>
              <button mat-raised-button
                      [disabled]="!hasIndexer || processing"
                      (click)="reprocess('MissingThumbnail')">
                Reprocess Missing Thumbnails
              </button>
              <button mat-raised-button color="accent"
                      [disabled]="!hasIndexer || processing"
                      (click)="reprocess('Heic')">
                Reprocess HEIC Files
              </button>
              <button mat-raised-button color="warn"
                      [disabled]="!hasIndexer || processing"
                      (click)="reprocess('Failed')">
                Retry Failed Files
              </button>
            </div>
          </section>

          <!-- Progress -->
          @if (processing) {
            <section class="progress">
              <mat-progress-bar mode="indeterminate"></mat-progress-bar>
              <p>Processing {{ queuedCount }} files...</p>
            </section>
          }
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .admin-container { padding: 16px; max-width: 800px; margin: 0 auto; }
    section { margin-bottom: 24px; }
    h3 { margin-bottom: 12px; color: #666; }
    .indexers { display: flex; gap: 8px; flex-wrap: wrap; }
    .warning { color: #f57c00; display: flex; align-items: center; gap: 8px; }
    .stat-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 16px; }
    .stat-item { text-align: center; padding: 16px; background: #f5f5f5; border-radius: 8px; }
    .stat-value { display: block; font-size: 2rem; font-weight: bold; }
    .stat-label { color: #666; }
    .action-buttons { display: flex; gap: 8px; flex-wrap: wrap; }
    .progress { margin-top: 16px; }
  `]
})
export class AdminComponent implements OnInit, OnDestroy {
  private reprocessService = inject(ReprocessService);
  private snackBar = inject(MatSnackBar);

  indexers$ = this.reprocessService.indexers$;
  stats?: ReprocessStats;
  processing = false;
  queuedCount = 0;
  hasIndexer = false;

  async ngOnInit() {
    await this.reprocessService.connect();
    this.loadStats();

    this.indexers$.subscribe(indexers => {
      this.hasIndexer = indexers.length > 0;
    });
  }

  ngOnDestroy() {
    this.reprocessService.disconnect();
  }

  async loadStats() {
    this.stats = await firstValueFrom(this.reprocessService.getStats());
  }

  async reprocess(filter: 'MissingMetadata' | 'MissingThumbnail' | 'Failed' | 'Heic') {
    this.processing = true;
    try {
      const result = await firstValueFrom(this.reprocessService.reprocessByFilter(filter, 500));
      if (result.success) {
        this.queuedCount = result.queuedCount;
        this.snackBar.open(`Queued ${result.queuedCount} files for reprocessing`, 'OK', { duration: 5000 });
      } else {
        this.snackBar.open(`Error: ${result.error}`, 'OK', { duration: 5000 });
        this.processing = false;
      }
    } catch (error) {
      this.snackBar.open('Failed to start reprocessing', 'OK', { duration: 5000 });
      this.processing = false;
    }
  }
}
```

---

## File Summary

| Phase | File | Action |
|-------|------|--------|
| 1 | `src/Database/Entities/IndexedFile.cs` | Modify (add timestamps) |
| 1 | `src/Api/Hubs/IndexerHub.cs` | Create |
| 1 | `src/Api/Services/ReprocessService.cs` | Create |
| 1 | `src/Api/Controllers/ReprocessController.cs` | Create |
| 1 | `src/Api/Consumers/MetadataExtractedConsumer.cs` | Modify (set timestamp) |
| 1 | `src/Api/Consumers/ThumbnailGeneratedConsumer.cs` | Modify (set timestamp) |
| 1 | `src/Api/Program.cs` | Modify (add SignalR) |
| 2 | `src/IndexingService/IndexingService.csproj` | Modify (add package) |
| 2 | `src/IndexingService/Services/SignalRClientService.cs` | Create |
| 2 | `src/IndexingService/Services/SignalRHostedService.cs` | Create |
| 2 | `src/IndexingService/Program.cs` | Modify |
| 3 | `src/Web/package.json` | Modify (add @microsoft/signalr) |
| 3 | `src/Web/src/app/services/reprocess.service.ts` | Create |
| 3 | `src/Web/src/app/components/admin/admin.component.ts` | Create |

## Dependencies

**API:** None (SignalR included in ASP.NET Core)

**Indexer:**
```bash
dotnet add src/IndexingService package Microsoft.AspNetCore.SignalR.Client
```

**Web:**
```bash
cd src/Web && npm install @microsoft/signalr
```

## Testing

1. Unit tests for `ReprocessService`
2. Integration test for SignalR hub connectivity
3. E2E test: trigger reprocess from UI, verify file re-ingested
4. Load test: bulk reprocess 1000 files

## Rollout

1. Run database migration
2. Deploy API (backwards compatible)
3. Deploy Indexer with SignalR client
4. Deploy Web UI
5. Test single file reprocess
6. Bulk reprocess HEIC files (27k)
