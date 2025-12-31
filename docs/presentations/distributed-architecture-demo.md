---
marp: true
theme: default
paginate: true
backgroundColor: #ffffff
color: #000000
style: |
  section {
    font-family: 'Segoe UI', Arial, sans-serif;
    font-size: 24px;
  }
  h1 {
    color: #000000;
    font-size: 1.8em;
  }
  h2 {
    color: #333333;
    font-size: 1.3em;
  }
  code {
    background: #f5f5f5;
    color: #333;
    font-size: 0.75em;
  }
  pre {
    font-size: 0.7em;
  }
  table {
    font-size: 0.7em;
  }
  strong {
    color: #000;
  }
  .small {
    font-size: 0.8em;
  }
---

# Photos Index

## Distributed Photo Processing & Deduplication

.NET 10, Angular 21, RabbitMQ, SignalR, OpenTelemetry

**v0.10.0** - December 2025

---

# The Starting Point

## Traditional Architecture

```
┌─────────────────────────────────────┐
│            Single Server            │
│  ┌─────┐  ┌─────┐  ┌──────────┐    │
│  │ SPA │──│ API │──│ Database │    │
│  └─────┘  └─────┘  └──────────┘    │
│           IIS / Kestrel             │
└─────────────────────────────────────┘
```

**Problems:**
- CPU-bound tasks block API
- No horizontal scaling
- Single point of failure

---

# The Challenge

## Photo Indexing at Scale

- **72,000+ photos** to process
- Extract EXIF metadata (CPU intensive)
- Generate thumbnails (CPU intensive)
- Compute SHA256 hashes (I/O intensive)
- **Find and manage duplicates**

**Constraint:** Run on home NAS hardware

---

# The Solution

## Distributed Message-Driven Architecture

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Indexer   │◄───►│     API     │────▶│    MinIO    │
│  (Synology) │     │  (TrueNAS)  │     │  (Storage)  │
└─────────────┘     └──────┬──────┘     └─────────────┘
    SignalR                │ Publish
                           ▼
                    ┌─────────────┐
                    │  RabbitMQ   │
                    └──────┬──────┘
         ┌─────────────────┼─────────────────┐
         ▼                 ▼                 ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────┐
│ MetadataService │ │ThumbnailService │ │CleanerService│
└─────────────────┘ └─────────────────┘ └─────────────┘
```

---

# Technology Stack

| Layer | Technology |
|-------|------------|
| **Frontend** | Angular 21 with Signals |
| **API** | .NET 10 / ASP.NET Core |
| **Real-time** | SignalR (bidirectional) |
| **Messaging** | RabbitMQ + MassTransit |
| **Storage** | MinIO (S3-compatible) |
| **Database** | PostgreSQL + EF Core |
| **Observability** | Jaeger + OpenTelemetry |
| **Proxy** | Traefik |

---

# Services Overview

## 5 Microservices

| Service | Role | Features |
|---------|------|----------|
| **API** | REST + SignalR hub | CRUD, real-time events |
| **Indexer** | File discovery | Scan, hash, upload |
| **Metadata** | EXIF extraction | Camera, GPS, dates |
| **Thumbnail** | Image processing | Resize, optimize |
| **Cleaner** | Safe deletion | Dry-run, archive, undo |

---

# Key Concept #1: Message-Driven

## Publish/Subscribe with MassTransit

```csharp
// API publishes when file is uploaded
await _publishEndpoint.Publish(new FileDiscoveredMessage
{
    CorrelationId = Guid.NewGuid(),
    IndexedFileId = fileId,
    FileHash = hash
});
```

**Benefits:** Async, automatic retries, dead letter queues

---

# Key Concept #2: Fan-Out Pattern

## One Message, Multiple Consumers

```
FileDiscoveredMessage
        │
   ┌────┼────┬────────┐
   ▼    ▼    ▼        ▼
Queue  Queue Queue   Queue
   │    │    │        │
   ▼    ▼    ▼        ▼
Meta  Thumb  AI     Future
Svc   Svc   Tag      Svc
```

Each service gets **its own copy** of every message.

---

# Key Concept #3: Real-Time with SignalR

## Bidirectional Communication

```
┌──────────┐      ┌─────────┐      ┌─────────────┐
│  Angular │◄────►│   API   │◄────►│   Indexer   │
│   (SPA)  │      │(SignalR)│      │  (Worker)   │
└──────────┘      └─────────┘      └─────────────┘
     │                 │                  │
     │  ScanProgress   │   IndexerStatus  │
     │◄────────────────│◄─────────────────│
     │                 │                  │
     │  DeletionStatus │   FileProcessed  │
     │◄────────────────│◄─────────────────│
```

**Real-time updates without polling!**

---

# SignalR: The Implementation

```csharp
// API Hub
public class IndexerHub : Hub
{
    public async Task ReportStatus(IndexerStatusDto status)
    {
        await Clients.All.SendAsync("IndexerStatusUpdate", status);
    }

    public async Task ReportProgress(ScanProgressDto progress)
    {
        await Clients.All.SendAsync("ScanProgressUpdate", progress);
    }
}

// Angular Service
this.connection.on('ScanProgressUpdate', (progress) => {
    this.scanProgress.set(progress);
});
```

---

# Key Concept #4: Distributed Tracing

## OpenTelemetry + Jaeger

```
Trace: abc123
├── POST /api/files/ingest
│   ├── PostgreSQL INSERT
│   ├── MinIO PUT
│   └── FileDiscoveredMessage send
├── FileDiscovered (MetadataService)
│   └── MetadataExtractedMessage send
├── FileDiscovered (ThumbnailService)
│   └── ThumbnailGeneratedMessage send
└── FileDiscovered (CleanerService)
    └── CleanupCompleteMessage send
```

---

# Cleaner Service

## Safe Duplicate Removal

```
┌─────────────┐
│   Trigger   │  API validates selection
└──────┬──────┘
       ▼
┌─────────────┐
│   Dry-Run   │  Preview changes (optional)
└──────┬──────┘
       ▼
┌─────────────┐
│   Archive   │  Move to trash directory
└──────┬──────┘
       ▼
┌─────────────┐
│  Database   │  Mark as deleted, log action
└──────┬──────┘
       ▼
┌─────────────┐
│   Notify    │  SignalR status update
└─────────────┘
```

---

# Duplicate Status Workflow

## 6-State Machine

```
┌─────────┐     ┌──────────────┐     ┌───────────┐
│ Pending │────►│ AutoSelected │────►│ Validated │
└─────────┘     └──────────────┘     └─────┬─────┘
                       │                   │
                       ▼                   ▼
                  ┌─────────┐        ┌──────────┐
                  │ Pending │◄───────│ Cleaning │
                  └─────────┘        └────┬─────┘
                       ▲           ┌──────┴──────┐
                       │           ▼             ▼
               ┌───────────────┐ ┌─────────┐
               │CleaningFailed │ │ Cleaned │
               └───────────────┘ └─────────┘
```

---

# Duplicate Management UI

## Power User Features

| Feature | Shortcut |
|---------|----------|
| Navigate files | ← → |
| Navigate groups | ↑ ↓ |
| Select original | Space / 1-9 |
| Auto-select | A |
| Validate | V |
| Execute cleanup | X |
| Undo | U |
| Help | ? |

---

# Gallery View

## Infinite Scroll with Filters

```
┌──────────────────────────────────────────┐
│ 🔍 Search    📷 Camera    📅 Date        │
├──────────────────────────────────────────┤
│ ┌────┐ ┌────┐ ┌────┐ ┌────┐ ┌────┐      │
│ │ 📷 │ │ 📷 │ │ 📷 │ │ 📷 │ │ 📷 │      │
│ └────┘ └────┘ └────┘ └────┘ └────┘      │
│ ┌────┐ ┌────┐ ┌────┐ ┌────┐ ┌────┐      │
│ │ 📷 │ │ 📷 │ │ 📷 │ │ 📷 │ │ 📷 │      │
│ └────┘ └────┘ └────┘ └────┘ └────┘      │
│              ↓ Loading...               │
└──────────────────────────────────────────┘
```

- Lazy loading with intersection observer
- Adjustable tile size
- Camera and date filters

---

# Real-World Results

## Resource Distribution

| Resource | Synology (Indexer) | TrueNAS (Services) |
|----------|-------------------|-------------------|
| **CPU** | 3% | 40-60% |
| **Memory** | 22% | 33% |
| **Role** | Scan, hash, upload | Process, store |

Perfect workload distribution!

---

# Performance on Home Hardware

## 100+ files/minute on NAS boxes

| Hardware | Specs | Role |
|----------|-------|------|
| **Synology DS920+** | Intel J4125, 4GB RAM | Indexer |
| **TrueNAS Mini** | Intel Xeon, 32GB RAM | Services |
| **Network** | 1Gbps LAN | File transfer |

**72,000 photos in ~12 hours** - overnight batch processing!

---

# What Happens Per File

## Full pipeline in ~500ms

1. **Indexer**: Scan, SHA256 hash, HTTP upload
2. **API**: Store metadata, 2x MinIO upload, publish message
3. **RabbitMQ**: Fan-out to queues
4. **MetadataService**: Download, EXIF extract, delete temp
5. **ThumbnailService**: Download, resize, upload
6. **SignalR**: Real-time progress to UI

All traced end-to-end in Jaeger!

---

# Incremental Indexing

## Scan Sessions

```sql
-- Track what's been scanned
CREATE TABLE "ScanSessions" (
    "Id" UUID PRIMARY KEY,
    "DirectoryId" UUID NOT NULL,
    "StartedAt" TIMESTAMPTZ,
    "CompletedAt" TIMESTAMPTZ,
    "FilesFound" INT,
    "FilesProcessed" INT,
    "Status" VARCHAR(20) -- Scanning, Completed, Failed
);
```

Only process **new and modified** files!

---

# Architecture Decisions

## ADRs (Architecture Decision Records)

| ADR | Decision |
|-----|----------|
| 007 | MassTransit for messaging |
| 008 | SignalR for real-time |
| 012 | Incremental indexing |
| 013 | Cleaner service architecture |
| 014 | Status workflow enum |
| 015 | Auth with external IDP (planned) |

---

# Lessons Learned

| Version | Bug | Root Cause |
|---------|-----|------------|
| v0.3.5 | DateTime save fails | Kind=Unspecified |
| v0.3.6 | Metadata OR thumbnail | Competing consumers |
| v0.3.8 | Images bucket fills up | No cleanup |
| v0.9.0 | SignalR disconnect | No reconnection |
| v0.10.0 | Status magic strings | No enum validation |

**Observability made debugging easy.**

---

# Future: Fan-Out Extensibility

## Add consumers without changing publisher

```
FileDiscoveredMessage
        │
   ┌────┴────┬────────┬────────┬────────┐
   ▼         ▼        ▼        ▼        ▼
Metadata  Thumbnail  Vector   Face     AI
Service   Service    Embed    Detect   Tag
(today)   (today)    (CLIP)   (YOLO)   (LLM)
```

**Zero code changes to API** - just deploy new consumers!

---

# Future: Authentication

## Planned for v0.15.0+

```
┌───────────────┐     ┌─────────────────┐
│  Infomaniak   │     │  Photos Index   │
│    Login      │◄───►│     (OIDC)      │
│   (OAuth2)    │     │                 │
└───────────────┘     └─────────────────┘
```

- OpenID Connect with external IDP
- Role-Based Access Control (RBAC)
- 4 groups, 6 roles, 17 permissions
- Complete audit trail

---

# Roadmap

| Version | Features |
|---------|----------|
| **v0.10.0** ✅ | Status workflow, UX improvements |
| v0.11.0 | Accessibility, skeleton loading |
| v0.12.0 | Navigation, design system |
| v0.15.0 | Authentication (OIDC) |
| v0.16.0 | Authorization (RBAC) |
| v1.0.0 | Production-ready release |

---

# Architecture Benefits

| Aspect | Monolith | Distributed |
|--------|----------|-------------|
| Scaling | Vertical only | Horizontal |
| Failures | Full outage | Partial |
| Debugging | Log files | Traces |
| Deployment | Full redeploy | Per-service |
| Real-time | Polling | SignalR push |

---

# When NOT to Use This

- **Small apps** - Monolith is fine
- **Tight deadlines** - More risk
- **Team unfamiliar** - Learning curve

**Use when:**
- CPU-bound background processing
- Need independent scaling
- Real-time updates required
- Observability is critical

---

# Demo Time!

- **Jaeger UI:** Distributed traces
- **RabbitMQ:** Queue stats
- **Grafana:** Logs aggregation
- **Web App:** Photo browser & duplicates
- **SignalR:** Real-time progress

All running on two NAS boxes at home!

---

# Resources

- **MassTransit:** masstransit.io
- **SignalR:** docs.microsoft.com/signalr
- **OpenTelemetry .NET:** opentelemetry.io/docs/instrumentation/net
- **Jaeger:** jaegertracing.io
- **This Project:** github.com/gbolabs/photos-index

---

# Key Takeaways

1. **Message-driven** decouples CPU-bound work
2. **Fan-out** enables parallel processing
3. **SignalR** for real-time bidirectional updates
4. **Distributed tracing** is essential
5. **State machines** enforce business rules
6. **Start simple**, add complexity when needed

**"Make it work, make it right, make it fast"**

---

# Questions?

## github.com/gbolabs/photos-index

```
┌─────────────────────────────────────────────────┐
│  Indexer ──► API ──► RabbitMQ ──► Services      │
│     ▲                    │                      │
│     │                    ▼                      │
│     └──────── SignalR ◄──┴──► Angular UI        │
└─────────────────────────────────────────────────┘
```

**v0.10.0** - December 2025
