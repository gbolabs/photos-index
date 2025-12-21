# 001: Real-time Scan Communication

**Priority**: P2 (Enhancement)
**Agent**: A2 (Indexing Service) + A3 (Web UI)
**Branch**: `feature/realtime-scan`
**Estimated Complexity**: High

## Overview

Implement real-time communication between the Web UI and Indexing Service to enable:
- "Scan Now" button triggering immediate scans
- Live progress updates during scanning
- Real-time notifications when scans complete
- Dashboard auto-refresh on data changes

## Current State

- Indexing Service runs on a timer (INDEXING_INTERVAL_MINUTES)
- No way to trigger immediate scans from UI
- Dashboard uses polling for updates (10-second interval)
- No progress visibility during scans

## Proposed Solutions

### Option 1: SignalR (Recommended for .NET)

**Pros:**
- Native .NET support
- Automatic reconnection
- Works with existing ASP.NET Core infrastructure
- Supports WebSockets, Server-Sent Events, Long Polling fallbacks
- Built-in groups for broadcasting to specific clients

**Cons:**
- Requires SignalR hub in API
- Angular client library needed (@microsoft/signalr)

**Implementation:**
```
Web UI ←→ SignalR Hub (API) ←→ Indexing Service
                ↓
          PostgreSQL (scan status)
```

### Option 2: Aspire-Supported Messaging

**Azure Service Bus / RabbitMQ via Aspire:**

**Pros:**
- Aspire has built-in support for messaging
- Decoupled architecture
- Reliable message delivery
- Supports pub/sub patterns

**Cons:**
- Additional infrastructure (RabbitMQ container)
- More complex setup
- Overkill for single-user NAS deployment

**Implementation:**
```
Web UI → API → Message Queue → Indexing Service
                    ↑
            Progress Messages
```

### Option 3: Dapr (Cloud-Native)

**Pros:**
- Sidecar pattern - no code changes for messaging
- Supports multiple backends (Redis, RabbitMQ, Kafka)
- Built-in pub/sub, state management
- Service-to-service invocation

**Cons:**
- Requires Dapr sidecars for each service
- Higher resource usage
- More complex Kubernetes/Podman setup
- Overkill for current scope

### Option 4: Simple Database Polling + SSE

**Pros:**
- No additional infrastructure
- Simple implementation
- Works with current architecture

**Cons:**
- Not truly real-time
- Database polling overhead
- Less responsive UX

## Recommended Approach: SignalR

For a Synology NAS deployment, SignalR provides the best balance:

1. **No additional containers** - runs within API service
2. **Real-time updates** - WebSocket-based
3. **Angular support** - @microsoft/signalr npm package
4. **Aspire integration** - Works with existing telemetry

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        Web Browser                          │
│  ┌─────────────────────────────────────────────────────┐   │
│  │              Angular Application                     │   │
│  │  - ScanHub.connection.on('scanProgress', ...)       │   │
│  │  - ScanHub.connection.invoke('startScan', dirId)    │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ WebSocket
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                        API Service                          │
│  ┌─────────────────────────────────────────────────────┐   │
│  │                   ScanHub : Hub                      │   │
│  │  - StartScan(directoryId)                           │   │
│  │  - Clients.All.SendAsync("scanProgress", ...)       │   │
│  │  - Clients.All.SendAsync("scanComplete", ...)       │   │
│  └─────────────────────────────────────────────────────┘   │
│                              │                              │
│                              │ IHubContext<ScanHub>         │
│                              ▼                              │
│  ┌─────────────────────────────────────────────────────┐   │
│  │              IScanNotificationService                │   │
│  │  - NotifyProgress(dirId, current, total)            │   │
│  │  - NotifyScanStarted(dirId)                         │   │
│  │  - NotifyScanComplete(dirId, filesFound)            │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ HTTP API Call
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    Indexing Service                         │
│  - Calls API endpoints to report progress                  │
│  - POST /api/internal/scan-progress                        │
│  - API broadcasts via SignalR                              │
└─────────────────────────────────────────────────────────────┘
```

## Implementation Tasks

### API Changes

1. **Add SignalR package**
   ```xml
   <PackageReference Include="Microsoft.AspNetCore.SignalR" />
   ```

2. **Create ScanHub**
   ```csharp
   public class ScanHub : Hub
   {
       public async Task JoinScanUpdates(string directoryId)
       {
           await Groups.AddToGroupAsync(Context.ConnectionId, $"scan-{directoryId}");
       }
   }
   ```

3. **Create IScanNotificationService**
   - Inject IHubContext<ScanHub>
   - Methods for broadcasting scan events

4. **Add internal API endpoints**
   - POST /api/internal/scan-started
   - POST /api/internal/scan-progress
   - POST /api/internal/scan-complete

### Indexing Service Changes

1. **Add HttpClient for progress reporting**
2. **Report progress during file scanning**
3. **Report completion with statistics**

### Angular Changes

1. **Install SignalR client**
   ```bash
   npm install @microsoft/signalr
   ```

2. **Create ScanHubService**
   ```typescript
   @Injectable({ providedIn: 'root' })
   export class ScanHubService {
     private hubConnection: HubConnection;

     scanProgress$ = new Subject<ScanProgress>();
     scanComplete$ = new Subject<ScanComplete>();
   }
   ```

3. **Update Indexing component**
   - Real-time progress bars
   - Live file count updates
   - Toast notifications on completion

4. **Update Dashboard component**
   - Auto-refresh statistics on scan complete
   - Show "Scanning..." indicator

## Message Types

```typescript
interface ScanStarted {
  directoryId: string;
  directoryPath: string;
  startedAt: string;
}

interface ScanProgress {
  directoryId: string;
  currentFile: string;
  filesProcessed: number;
  filesTotal: number;
  bytesProcessed: number;
  percentComplete: number;
}

interface ScanComplete {
  directoryId: string;
  filesFound: number;
  filesNew: number;
  filesUpdated: number;
  duplicatesDetected: number;
  duration: string;
  completedAt: string;
}

interface ScanError {
  directoryId: string;
  error: string;
  failedFile?: string;
}
```

## Acceptance Criteria

- [ ] "Scan Now" button triggers immediate scan
- [ ] Progress bar shows real-time scan progress
- [ ] File count updates live during scan
- [ ] Notification appears when scan completes
- [ ] Dashboard auto-refreshes after scan
- [ ] Multiple clients see same progress
- [ ] Works with Traefik reverse proxy
- [ ] Reconnects automatically on disconnect

## Future Enhancements

1. **Scan queue** - Queue multiple directories
2. **Cancel scan** - Ability to stop running scans
3. **Scan history** - View past scan results
4. **Scheduled scans** - Cron-like scheduling UI
5. **Dapr migration** - If scaling to multiple instances

## Dependencies

- SignalR NuGet package
- @microsoft/signalr npm package
- Traefik WebSocket configuration

## Estimated Effort

- API SignalR setup: 4 hours
- Indexing Service changes: 2 hours
- Angular SignalR client: 4 hours
- UI components: 4 hours
- Testing & integration: 4 hours
- **Total: ~18 hours**

## Alternative: RabbitMQ (Backlog Item 09-003)

If multi-instance scaling is needed, consider RabbitMQ approach from backlog item 09-003 instead of SignalR. RabbitMQ provides:
- Reliable message delivery
- Multiple consumer support
- Dead letter queues for failed messages
- Aspire integration

However, for single-user NAS deployment, SignalR is simpler and sufficient.
