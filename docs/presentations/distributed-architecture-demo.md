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

# Distributed Photo Processing

## From Monolith to Message-Driven Microservices

.NET 10, RabbitMQ, OpenTelemetry

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

**Constraint:** Run on home NAS hardware

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
         ┌────────────────┴────────────────┐
         ▼                                 ▼
┌─────────────────┐             ┌─────────────────┐
│ MetadataService │             │ThumbnailService │
└─────────────────┘             └─────────────────┘
```

---

# Technology Stack

| Layer | Technology |
|-------|------------|
| **Frontend** | Angular 21 |
| **API** | .NET 10 / ASP.NET Core |
| **Messaging** | RabbitMQ + MassTransit |
| **Storage** | MinIO (S3-compatible) |
| **Database** | PostgreSQL |
| **Observability** | Jaeger + OpenTelemetry |
| **Proxy** | Traefik |

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

**Critical:** Unique queue name per service type.

---

# Gotcha: Competing Consumers

## Same queue name = round-robin

```
Publisher → Queue → Instance1
                  → Instance2  (round-robin)
                  → Instance3
```

**Bug:** Both services used `FileDiscovered` queue
Each file got metadata **OR** thumbnail, never both!

**Fix:** Explicit unique queue names.

---

# Key Concept #3: Distributed Tracing

## OpenTelemetry + Jaeger

```
Trace: abc123
├── POST /api/files/ingest
│   ├── PostgreSQL INSERT
│   ├── MinIO PUT
│   └── FileDiscoveredMessage send
├── FileDiscovered (MetadataService)
│   └── MetadataExtractedMessage send
└── FileDiscovered (ThumbnailService)
    └── ThumbnailGeneratedMessage send
```

---

# Tracing: One Line of Code

```csharp
builder.AddPhotosIndexTelemetry("photos-index-api");
```

**Auto-instrumentation:**
- ASP.NET Core
- EF Core / PostgreSQL
- HTTP clients
- MassTransit (propagates trace context!)

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
3. **RabbitMQ**: Fan-out to 2 queues
4. **MetadataService**: Download, EXIF extract, delete
5. **ThumbnailService**: Download, resize, upload, delete
6. **API**: Receive results, update PostgreSQL

All traced end-to-end in Jaeger!

---

# Key Concept #4: Resource Cleanup

## Per-Service Object Keys

```
API uploads two copies:
├── images/metadata/{hash}   → MetadataService deletes
└── images/thumbnail/{hash}  → ThumbnailService deletes

Result: Only thumbnails persist!
```

**No coordination needed** - each service manages its own cleanup.

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

# Future Consumer Ideas

| Service | Technology | Purpose |
|---------|------------|---------|
| **VectorService** | CLIP embeddings | Semantic search |
| **FaceService** | YOLO / InsightFace | Face detection |
| **AITagService** | LLM (Ollama) | Auto-tagging |
| **GeoService** | Reverse geocoding | Location names |
| **DuplicateService** | pHash | Visual similarity |

Each runs independently, scales independently.

---

# Lessons Learned

| Version | Bug | Root Cause |
|---------|-----|------------|
| v0.3.5 | DateTime save fails | Kind=Unspecified |
| v0.3.6 | Metadata OR thumbnail | Competing consumers |
| v0.3.7 | @eaDir indexed | Missing exclusion |
| v0.3.8 | Images bucket fills up | No cleanup |

**Observability made debugging easy.**

---

# Architecture Benefits

| Aspect | Monolith | Distributed |
|--------|----------|-------------|
| Scaling | Vertical only | Horizontal |
| Failures | Full outage | Partial |
| Debugging | Log files | Traces |
| Deployment | Full redeploy | Per-service |

---

# When NOT to Use This

- **Small apps** - Monolith is fine
- **Tight deadlines** - More risk
- **Team unfamiliar** - Learning curve

**Use when:**
- CPU-bound background processing
- Need independent scaling
- Observability is critical

---

# Getting Started

1. `dotnet add package MassTransit.RabbitMQ`

2. Create a message:
   ```csharp
   public record OrderCreated { public Guid OrderId { get; init; } }
   ```

3. Publish from API, consume in worker

4. Add OpenTelemetry for visibility

---

# Demo Time!

- **Jaeger UI:** Distributed traces
- **RabbitMQ:** Queue stats
- **Grafana:** Logs aggregation
- **Web App:** Photo browser

All running on two NAS boxes at home!

---

# Resources

- **MassTransit:** masstransit.io
- **OpenTelemetry .NET:** opentelemetry.io/docs/instrumentation/net
- **Jaeger:** jaegertracing.io
- **This Project:** github.com/gbolabs/photos-index

---

# Key Takeaways

1. **Message-driven** decouples CPU-bound work
2. **Fan-out** enables parallel processing
3. **Distributed tracing** is essential
4. **Start simple**, add complexity when needed

**"Make it work, make it right, make it fast"**
