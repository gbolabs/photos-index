---
marp: true
theme: default
paginate: true
backgroundColor: #1a1a2e
color: #eee
style: |
  section {
    font-family: 'Segoe UI', sans-serif;
  }
  h1, h2 {
    color: #00d4ff;
  }
  code {
    background: #2d2d44;
    color: #7dd3fc;
  }
  table {
    font-size: 0.8em;
  }
  .columns {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 1rem;
  }
---

# Distributed Photo Processing
## From Monolith to Message-Driven Microservices

**A practical journey with .NET 10, RabbitMQ, and OpenTelemetry**

---

# The Starting Point

## Traditional Architecture (Sound familiar?)

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
- CPU-bound tasks (image processing) block API
- No horizontal scaling
- Single point of failure
- Hard to observe what's happening

---

# The Challenge

## Photo Indexing at Scale

- **72,000+ photos** to process
- **Extract EXIF metadata** (CPU intensive)
- **Generate thumbnails** (CPU intensive)
- **Compute SHA256 hashes** (I/O intensive)
- **Store in PostgreSQL + Object Storage**

**Constraint:** Run on home NAS hardware (Synology + TrueNAS)

---

# The Solution

## Distributed Message-Driven Architecture

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Indexer   │────▶│     API     │────▶│    MinIO    │
│  (Synology) │     │  (TrueNAS)  │     │  (Storage)  │
└─────────────┘     └──────┬──────┘     └─────────────┘
                          │ Publish
                          ▼
                   ┌─────────────┐
                   │  RabbitMQ   │
                   └──────┬──────┘
                          │ Fan-out
            ┌─────────────┴─────────────┐
            ▼                           ▼
   ┌─────────────────┐       ┌─────────────────┐
   │ MetadataService │       │ThumbnailService │
   └─────────────────┘       └─────────────────┘
```

---

# Technology Stack

| Layer | Technology | Purpose |
|-------|------------|---------|
| **Frontend** | Angular 21 | SPA with Material Design |
| **API** | .NET 10 / ASP.NET Core | REST API, EF Core |
| **Messaging** | RabbitMQ + MassTransit | Async message broker |
| **Storage** | MinIO | S3-compatible object storage |
| **Database** | PostgreSQL | Relational data |
| **Observability** | Jaeger + OpenTelemetry | Distributed tracing |
| **Proxy** | Traefik | Reverse proxy, routing |
| **Container** | Docker Compose | Orchestration |

---

# Key Concept #1: Message-Driven

## Publish/Subscribe with MassTransit

```csharp
// API publishes when file is uploaded
await _publishEndpoint.Publish(new FileDiscoveredMessage
{
    CorrelationId = Guid.NewGuid(),
    IndexedFileId = fileId,
    ObjectKey = $"files/{hash}",
    FileHash = hash
});
```

**Benefits:**
- Sender doesn't wait for processing
- Retries handled automatically
- Dead letter queues for failures

---

# Key Concept #2: Fan-Out Pattern

## One Message → Multiple Consumers

```
FileDiscoveredMessage
        │
        ▼
   ┌─────────┐
   │Exchange │ (fanout)
   └────┬────┘
        │
   ┌────┴────┐
   ▼         ▼
Queue A    Queue B
   │         │
   ▼         ▼
Metadata  Thumbnail
Service   Service
```

Each service gets **its own copy** of every message.

---

# Fan-Out: The Implementation

```csharp
// MetadataService/Program.cs
cfg.ReceiveEndpoint("metadata-file-discovered", e =>
{
    e.ConfigureConsumer<FileDiscoveredConsumer>(context);
});

// ThumbnailService/Program.cs
cfg.ReceiveEndpoint("thumbnail-file-discovered", e =>
{
    e.ConfigureConsumer<FileDiscoveredConsumer>(context);
});
```

**Critical:** Each service needs a **unique queue name** to receive all messages.

---

# Gotcha: Competing Consumers

## What happens with same queue name?

```
                    → Instance1
Publisher → Queue  → Instance2  (round-robin)
                    → Instance3
```

**Bug we hit:** Both services used queue name `FileDiscovered`
→ Each file got **metadata OR thumbnail**, never both!

**Fix:** Explicit unique queue names per service type.

---

# Key Concept #3: Distributed Tracing

## OpenTelemetry + Jaeger

```
Trace: abc123
├── POST /api/files/ingest (Indexer → API)
│   ├── PostgreSQL INSERT
│   ├── MinIO PUT
│   └── FileDiscoveredMessage send
├── FileDiscovered receive (MetadataService)
│   ├── MinIO GET
│   └── MetadataExtractedMessage send
├── FileDiscovered receive (ThumbnailService)
│   ├── MinIO GET
│   ├── MinIO PUT (thumbnail)
│   └── ThumbnailGeneratedMessage send
└── MetadataExtracted receive (API)
    └── PostgreSQL UPDATE
```

---

# Tracing: One Line of Code

```csharp
// Program.cs - that's it!
builder.AddPhotosIndexTelemetry("photos-index-api");
```

**Under the hood:**
- OpenTelemetry SDK
- OTLP exporter to Jaeger
- Auto-instrumentation for:
  - ASP.NET Core
  - EF Core / PostgreSQL
  - HTTP clients
  - MassTransit (propagates trace context through messages!)

---

# Real-World Results

## Resource Distribution During 72K Photo Scan

| Resource | Synology (Indexer) | TrueNAS (Services) |
|----------|-------------------|-------------------|
| **CPU** | **3%** | 40-60% |
| **Memory** | 22% | 33% |
| **Role** | Scan, hash, upload | Process, store |

**Synology:** Light work (file I/O)
**TrueNAS:** Heavy work (ImageSharp, PostgreSQL)

Perfect workload distribution!

---

# Trace Visualization

## 32 Spans, 5 Services, 82ms

```
┌──────────────────────────────────────────────────┐
│ photos-index-traefik     ████                    │
│ photos-index-indexer     ██████████              │
│ photos-index-api         ████████████████        │
│ photos-index-metadata        ████████████        │
│ photos-index-thumbnail       ████████████        │
└──────────────────────────────────────────────────┘
                    Time →
```

See the **full request lifecycle** across all services in one view.

---

# Infrastructure as Code

## Docker Compose (excerpt)

```yaml
services:
  api:
    image: ghcr.io/gbolabs/photos-index/api:0.3.7
    environment:
      OTEL_EXPORTER_OTLP_ENDPOINT: http://jaeger:4317
      RabbitMQ__Host: rabbitmq
    depends_on:
      rabbitmq:
        condition: service_healthy

  metadata-service:
    image: ghcr.io/gbolabs/photos-index/metadata-service:0.3.7
    # Same RabbitMQ, different queue
```

---

# CI/CD: Tag → Release

```bash
git tag v0.3.7
git push origin v0.3.7
```

**GitHub Actions automatically:**
1. Builds all container images (parallel)
2. Pushes to GitHub Container Registry
3. Creates GitHub Release

```yaml
# .github/workflows/release.yml
on:
  push:
    tags: ['v*']
```

---

# Lessons Learned

## What We Got Wrong (and Fixed)

| Version | Bug | Root Cause |
|---------|-----|------------|
| v0.3.5 | DateTime save fails | `Kind=Unspecified` vs PostgreSQL `timestamptz` |
| v0.3.6 | Files get metadata OR thumbnail | Competing consumers (same queue name) |
| v0.3.7 | Synology @eaDir indexed | Missing directory exclusion filter |

**Observability made debugging easy** - Jaeger showed exactly where failures occurred.

---

# Architecture Benefits

## vs Traditional IIS Deployment

| Aspect | Monolith | Distributed |
|--------|----------|-------------|
| **Scaling** | Vertical only | Horizontal per service |
| **Failures** | Full outage | Partial degradation |
| **Debugging** | Log files | Distributed traces |
| **Deployment** | Full redeploy | Per-service updates |
| **Resource usage** | Single machine | Spread across nodes |

---

# When NOT to Use This

## Complexity has a cost

- **Small apps** - Monolith is fine
- **Tight deadlines** - More moving parts = more risk
- **Team unfamiliar** - Learning curve is real
- **Simple CRUD** - Overkill for basic operations

**Use when:**
- CPU-bound background processing
- Need independent scaling
- Multiple teams/services
- Observability is critical

---

# Getting Started

## Minimal Message-Driven Setup

1. **Add MassTransit + RabbitMQ**
   ```bash
   dotnet add package MassTransit.RabbitMQ
   ```

2. **Create a message**
   ```csharp
   public record OrderCreated { public Guid OrderId { get; init; } }
   ```

3. **Publish from API, consume in worker**

4. **Add OpenTelemetry** for visibility

---

# Demo Time!

## Live System

- **Jaeger UI:** Distributed traces
- **RabbitMQ Management:** Queue stats
- **Grafana:** Logs aggregation
- **Web App:** Photo browser with thumbnails

All running on two NAS boxes at home!

---

# Resources

- **MassTransit:** masstransit.io
- **OpenTelemetry .NET:** opentelemetry.io/docs/instrumentation/net
- **Jaeger:** jaegertracing.io
- **This Project:** github.com/gbolabs/photos-index

## Questions?

---

# Thank You!

## Key Takeaways

1. **Message-driven** decouples CPU-bound work from API
2. **Fan-out pattern** enables parallel processing
3. **Distributed tracing** is essential for debugging
4. **Start simple**, add complexity when needed

**"Make it work, make it right, make it fast"**
