# ADR-008: SignalR for API-Indexer Bidirectional Communication

**Status**: Accepted
**Date**: 2025-12-26
**Author**: Claude Code

## Context

Photos Index v0.3.x introduced distributed processing where:
- **Synology NAS** runs the Indexer (scans files, computes hashes, uploads to API)
- **TrueNAS** runs the API, MetadataService, ThumbnailService, and infrastructure

The current architecture has a limitation: the API cannot send commands back to the Indexer. This became apparent when we needed to re-process files that failed initial processing (e.g., 27k HEIC files that weren't processed due to missing decoder registration in v0.3.8).

### Problem

Files indexed before a bug fix remain unprocessed. To re-trigger processing:
1. The **Indexer** must re-read the file from disk (it has file access)
2. The **Indexer** must re-upload to the API (triggering the normal flow)

But currently:
- Indexer only **pushes** to API (HTTP POST)
- API cannot **push** commands to Indexer
- No way to request "please re-process file X"

### Options Considered

#### Option A: Indexer Polls API for Commands

```
Indexer → API: GET /api/commands (every N seconds)
        ← API: [{ reprocess: fileId, path: "/photos/x.heic" }]
```

**Rejected**: Polling is inefficient, adds latency (up to N seconds), and doesn't scale well.

#### Option B: Indexer Consumes from RabbitMQ

```
API → RabbitMQ: ReprocessCommand
    → Indexer: (new consumer)
```

**Rejected**: This would change the Indexer from a simple HTTP client to a message broker consumer, significantly increasing complexity. The Indexer is designed to be a lightweight, single-purpose service that only talks to the API.

#### Option C: SignalR Bidirectional Connection (Selected)

```
Indexer ←──SignalR──→ API
        ←──HTTP──────→
```

**Selected**: SignalR provides:
- Persistent WebSocket connection
- Real-time bidirectional communication
- Automatic reconnection
- Simple client library for .NET
- Keeps Indexer as an API client (no RabbitMQ)

## Decision

Add SignalR communication between the API and Indexer:

1. **API hosts a SignalR Hub** (`/hubs/indexer`)
2. **Indexer connects as a SignalR client** on startup
3. **API can push commands** to connected Indexer(s)
4. **Indexer reports status** back through the hub
5. **UI can observe** Indexer status and reprocess progress

### Architecture

```
┌─────────┐                    ┌─────────┐                    ┌──────────────┐
│   UI    │───HTTP POST───────▶│   API   │───RabbitMQ───────▶│ MetadataSvc  │
│(Angular)│◀──SignalR──────────│ (Hub)   │                    │ ThumbnailSvc │
└─────────┘                    └────┬────┘                    └──────────────┘
                                    │
                               SignalR (WebSocket)
                                    │
                               ┌────▼────┐
                               │ Indexer │
                               │(Synology)│
                               └─────────┘
```

### Communication Flow

#### Reprocess Command (API → Indexer)

```
1. UI: POST /api/reprocess/filter/Heic
2. API: Query DB for HEIC files missing metadata
3. API: Send SignalR "ReprocessFile" to all connected Indexers
4. Indexer: Receive command, read file from disk
5. Indexer: POST /api/files/ingest (normal flow)
6. API: Publish FileDiscoveredMessage to RabbitMQ
7. MetadataService + ThumbnailService: Process file
```

#### Status Reporting (Indexer → API → UI)

```
1. Indexer: hubConnection.InvokeAsync("ReportProgress", fileId, "reading")
2. API Hub: Forwards to UI clients group
3. UI: Updates progress indicator
```

### SignalR Hub Contract

```csharp
// Commands from API to Indexer
public interface IIndexerClient
{
    Task ReprocessFile(Guid fileId, string filePath);
    Task ReprocessFiles(IEnumerable<ReprocessFileRequest> files);
}

// Events from Indexer/API to UI
public interface IUIClient
{
    Task ReprocessProgress(Guid fileId, string status);
    Task ReprocessComplete(Guid fileId, bool success, string? error);
    Task IndexerConnected(string indexerId, string hostname);
    Task IndexerDisconnected(string indexerId);
}
```

### Indexer as Persistent SignalR Client

```csharp
// Indexer connects on startup
_hubConnection = new HubConnectionBuilder()
    .WithUrl($"{apiBaseUrl}/hubs/indexer")
    .WithAutomaticReconnect()
    .Build();

// Register command handlers
_hubConnection.On<Guid, string>("ReprocessFile", HandleReprocessFile);

await _hubConnection.StartAsync();
```

The Indexer:
- Connects to the API hub on startup
- Stays connected for the lifetime of the service
- Automatically reconnects on disconnection
- Reports its hostname/ID for multi-Indexer support

## Consequences

### Positive

- **No polling**: Real-time command delivery via WebSocket
- **Indexer stays simple**: Still just an API client (HTTP + SignalR), no RabbitMQ
- **Multi-Indexer support**: API can track multiple connected Indexers
- **UI visibility**: Users can see connected Indexers and reprocess progress
- **Reusable**: SignalR connection can be used for future features:
  - Live scan progress
  - Indexer health monitoring
  - Remote pause/resume
  - Configuration push

### Negative

- **Additional connection**: Indexer maintains WebSocket in addition to HTTP
- **Firewall considerations**: WebSocket must be allowed through any proxies
- **Complexity**: More code in both API and Indexer
- **State management**: API must track connected clients

### Neutral

- **RabbitMQ unchanged**: Processing services still use RabbitMQ exclusively
- **Backwards compatible**: Indexer can work without SignalR (just no reprocess capability)

## Alternatives Not Chosen

| Alternative | Reason Rejected |
|-------------|-----------------|
| gRPC streaming | Overkill for simple command/status, less browser-friendly |
| MQTT | Another broker to manage, SignalR already in .NET |
| Long polling | Inefficient, high latency |
| Shared database queue | Polling-based, not real-time |

## Implementation

See: `docs/plans/reindex-signalr-implementation.md`

### Files to Create/Modify

| Component | Files |
|-----------|-------|
| API | `Hubs/IndexerHub.cs`, `Services/ReprocessService.cs`, `Controllers/ReprocessController.cs` |
| Indexer | `Services/SignalRClientService.cs`, `Services/SignalRHostedService.cs` |
| Web | `services/reprocess.service.ts`, `components/admin/admin.component.ts` |

### Dependencies

- **API**: None (SignalR included in ASP.NET Core)
- **Indexer**: `Microsoft.AspNetCore.SignalR.Client`
- **Web**: `@microsoft/signalr`

## References

- [ASP.NET Core SignalR](https://docs.microsoft.com/en-us/aspnet/core/signalr/)
- [SignalR .NET Client](https://docs.microsoft.com/en-us/aspnet/core/signalr/dotnet-client)
- Implementation plan: `docs/plans/reindex-signalr-implementation.md`
- Related: ADR-007 (MassTransit for processing services)
