# ADR 007: MassTransit Messaging Patterns for Distributed Processing

## Status

Accepted

## Context

Photos Index v0.3.0 introduced distributed processing where:
- **Synology NAS** runs the Indexer (scans files, computes hashes)
- **TrueNAS** runs the API, MetadataService, ThumbnailService, and infrastructure

Files are uploaded to MinIO object storage, and services communicate via RabbitMQ using MassTransit.

During implementation, we encountered several issues that required understanding MassTransit's messaging patterns deeply.

## Decision

Use MassTransit with RabbitMQ following these patterns:

### 1. Message Types and Flow

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Indexer   │────▶│     API     │────▶│   MinIO     │
│  (Synology) │     │  (TrueNAS)  │     │  (Storage)  │
└─────────────┘     └──────┬──────┘     └─────────────┘
                          │
                          │ Publish FileDiscoveredMessage
                          ▼
                   ┌─────────────┐
                   │  RabbitMQ   │
                   │  Exchange   │
                   └──────┬──────┘
                          │
            ┌─────────────┴─────────────┐
            │                           │
            ▼                           ▼
  ┌─────────────────┐         ┌─────────────────┐
  │ metadata-file-  │         │ thumbnail-file- │
  │ discovered      │         │ discovered      │
  │ (Queue)         │         │ (Queue)         │
  └────────┬────────┘         └────────┬────────┘
           │                           │
           ▼                           ▼
  ┌─────────────────┐         ┌─────────────────┐
  │ MetadataService │         │ThumbnailService │
  └────────┬────────┘         └────────┬────────┘
           │                           │
           │ Publish                   │ Publish
           │ MetadataExtractedMessage  │ ThumbnailGeneratedMessage
           ▼                           ▼
  ┌─────────────────────────────────────────────┐
  │                  RabbitMQ                   │
  └─────────────────────┬───────────────────────┘
                        │
                        ▼
               ┌─────────────────┐
               │       API       │
               │   (Consumers)   │
               └─────────────────┘
                        │
                        ▼
               ┌─────────────────┐
               │   PostgreSQL    │
               └─────────────────┘
```

### 2. Message Definitions

```csharp
// Published by API when file is uploaded to MinIO
public record FileDiscoveredMessage
{
    public Guid CorrelationId { get; init; }
    public Guid IndexedFileId { get; init; }
    public Guid ScanDirectoryId { get; init; }
    public string FilePath { get; init; }
    public string FileHash { get; init; }
    public long FileSize { get; init; }
    public string ObjectKey { get; init; }  // MinIO path: "files/{hash}"
}

// Published by MetadataService after extracting EXIF
public record MetadataExtractedMessage
{
    public Guid CorrelationId { get; init; }
    public Guid IndexedFileId { get; init; }
    public bool Success { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public DateTime? DateTaken { get; init; }  // Must be UTC!
    public string? CameraMake { get; init; }
    // ... more EXIF fields
}

// Published by ThumbnailService after generating thumbnail
public record ThumbnailGeneratedMessage
{
    public Guid CorrelationId { get; init; }
    public Guid IndexedFileId { get; init; }
    public bool Success { get; init; }
    public string? ThumbnailObjectKey { get; init; }  // MinIO path
}
```

## Key Concepts

### Fan-Out Pattern (Pub/Sub)

When a message is **published**, MassTransit sends it to an **exchange** (not directly to a queue). Queues **bind** to the exchange and each receives a copy.

```
Publisher → Exchange → Queue1 → Consumer1
                    → Queue2 → Consumer2
                    → Queue3 → Consumer3
```

**Critical**: Each consumer that needs the message must have its **own queue**.

### Competing Consumers (Load Balancing)

When multiple instances of the SAME service share a queue, they **compete** for messages (each message goes to only one instance):

```
                    → Instance1
Publisher → Queue  → Instance2  (round-robin)
                    → Instance3
```

This is useful for horizontal scaling but NOT for multiple different services.

### The Bug We Found (v0.3.5)

Both MetadataService and ThumbnailService had consumers named `FileDiscoveredConsumer`. MassTransit's default `ConfigureEndpoints` creates queue names based on the consumer class name:

```csharp
// Both services had this:
x.AddConsumer<FileDiscoveredConsumer>();
cfg.ConfigureEndpoints(context);  // Creates queue "FileDiscovered"
```

Result: **Both services shared the same queue** → Competing consumers → Each file processed by only ONE service (metadata OR thumbnail, never both).

### The Fix (v0.3.6)

Use explicit, unique queue names:

```csharp
// MetadataService
cfg.ReceiveEndpoint("metadata-file-discovered", e =>
{
    e.ConfigureConsumer<FileDiscoveredConsumer>(context);
});

// ThumbnailService
cfg.ReceiveEndpoint("thumbnail-file-discovered", e =>
{
    e.ConfigureConsumer<FileDiscoveredConsumer>(context);
});
```

Now both queues bind to the `FileDiscoveredMessage` exchange and each receives every message.

## RabbitMQ Topology

After the fix, RabbitMQ creates this topology:

```
Exchanges:
├── Shared.Messages:FileDiscoveredMessage (fanout)
├── Shared.Messages:MetadataExtractedMessage (fanout)
└── Shared.Messages:ThumbnailGeneratedMessage (fanout)

Queues:
├── metadata-file-discovered
│   └── Bound to: FileDiscoveredMessage exchange
├── thumbnail-file-discovered
│   └── Bound to: FileDiscoveredMessage exchange
├── MetadataExtracted
│   └── Bound to: MetadataExtractedMessage exchange
└── ThumbnailGenerated
    └── Bound to: ThumbnailGeneratedMessage exchange
```

## DateTime and PostgreSQL

### The Bug (v0.3.4)

PostgreSQL's `timestamp with time zone` column requires `DateTime` values with `Kind=Utc`. EXIF dates parsed with default settings have `Kind=Unspecified`:

```csharp
// BAD: Kind=Unspecified
DateTime.TryParseExact(value, "yyyy:MM:dd HH:mm:ss",
    CultureInfo.InvariantCulture,
    DateTimeStyles.None,  // ← Problem!
    out result);

// Error: "Cannot write DateTime with Kind=Unspecified to PostgreSQL
//         type 'timestamp with time zone'"
```

### The Fix (v0.3.5)

Always specify UTC when parsing dates destined for PostgreSQL:

```csharp
// GOOD: Kind=Utc
DateTime.TryParseExact(value, "yyyy:MM:dd HH:mm:ss",
    CultureInfo.InvariantCulture,
    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
    out result);
```

## Distributed Processing Mode

### The Bug (v0.3.3)

IndexingOrchestrator used `BatchIngestFilesAsync` which sends metadata but **not file content**. The API only publishes `FileDiscoveredMessage` when file content is uploaded:

```csharp
// API's FileIngestService
if (request.FileContent is not null)  // ← Only with content!
{
    await _objectStorage.UploadAsync(...);
    await _publishEndpoint.Publish(new FileDiscoveredMessage {...});
}
```

### The Fix (v0.3.4)

Detect "distributed mode" and use `IngestFileWithContentAsync`:

```csharp
// Distributed mode: both local processing disabled
_isDistributedMode = !_options.ExtractMetadata && !_options.GenerateThumbnails;

if (_isDistributedMode)
{
    // Upload file content → triggers FileDiscoveredMessage
    await _apiClient.IngestFileWithContentAsync(request, fileStream, contentType, ct);
}
else
{
    // Local processing → send metadata only
    await _apiClient.BatchIngestFilesAsync(request, ct);
}
```

## Observability with Jaeger

MassTransit automatically propagates trace context through messages. A single trace shows the full flow:

```
Trace: abc123
├── POST api/files/ingest (Indexer → API)
│   ├── PostgreSQL SELECT/UPDATE
│   ├── MinIO PUT (upload file)
│   └── FileDiscoveredMessage send
├── FileDiscovered receive (MetadataService)
│   ├── MinIO GET (download file)
│   ├── Image processing
│   └── MetadataExtractedMessage send
├── FileDiscovered receive (ThumbnailService)
│   ├── MinIO GET (download file)
│   ├── Thumbnail generation
│   ├── MinIO PUT (upload thumbnail)
│   └── ThumbnailGeneratedMessage send
├── MetadataExtracted receive (API)
│   └── PostgreSQL UPDATE
└── ThumbnailGenerated receive (API)
    └── PostgreSQL UPDATE
```

Use Jaeger to debug message flow issues:
- Missing service in trace → Queue binding issue
- `otel.status_code: ERROR` → Check `otel.status_description`

## Best Practices

### 1. Unique Queue Names
Always use explicit queue names when multiple services consume the same message type:
```csharp
cfg.ReceiveEndpoint("servicename-message-type", e => {...});
```

### 2. UTC DateTime
Always use UTC for dates stored in PostgreSQL:
```csharp
DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
```

### 3. Correlation IDs
Include `CorrelationId` in all messages for tracing:
```csharp
public Guid CorrelationId { get; init; } = Guid.NewGuid();
```

### 4. Error Handling
Publish error results so the API can update database:
```csharp
catch (Exception ex)
{
    await _publishEndpoint.Publish(new MetadataExtractedMessage
    {
        Success = false,
        ErrorMessage = ex.Message
    });
}
```

### 5. Idempotent Consumers
Design consumers to handle duplicate messages (RabbitMQ at-least-once delivery):
```csharp
// Check if already processed
if (file.ThumbnailPath is not null) return;
```

## MinIO Thumbnail Access

### Storage Structure

```
MinIO Buckets:
├── images/
│   └── files/{hash}          # Original images uploaded by API
└── thumbnails/
    └── thumbs/{hash}.jpg     # Generated thumbnails
```

### Traefik Routing

```
Web Request                    Traefik                         MinIO
────────────────────────────────────────────────────────────────────────
/thumbnails/thumbs/{hash}.jpg  → (no strip prefix) →  /thumbnails/thumbs/{hash}.jpg
                                                       ↓
                                                bucket: thumbnails
                                                key: thumbs/{hash}.jpg
```

**Important**: Do NOT use strip-prefix middleware for thumbnail routes. MinIO path-style URLs include the bucket name in the path.

### Bucket Policy

MinIO buckets are private by default. To allow anonymous thumbnail access:

```bash
# Inside MinIO container
mc alias set local http://localhost:9000 minioadmin minioadmin
mc anonymous set download local/thumbnails
```

### Common Issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| 403 Forbidden | Bucket not public | `mc anonymous set download local/thumbnails` |
| 404 Not Found | Strip-prefix removing bucket name | Remove `stripprefix` middleware from Traefik |
| No thumbnail in UI | ThumbnailService not receiving messages | Check queue names (v0.3.6 fix) |

## Release History

| Version | Issue | Root Cause | Fix |
|---------|-------|------------|-----|
| v0.3.4 | Files not processed by MetadataService/ThumbnailService | IndexingOrchestrator didn't upload file content in distributed mode | Added `ProcessAndIngestDistributedAsync` with `IngestFileWithContentAsync` |
| v0.3.5 | MetadataService fails to save DateTaken | DateTime parsed with `Kind=Unspecified`, PostgreSQL requires UTC | Use `AssumeUniversal \| AdjustToUniversal` |
| v0.3.6 | Files get metadata OR thumbnail, not both | Services competed for same queue due to identical consumer names | Unique queue names per service |
| v0.3.6+ | Thumbnails return 403/404 | Traefik strip-prefix + private bucket | Remove strip-prefix, set bucket policy |
| v0.3.7 | Synology system folders indexed (@eaDir) | No exclusion for Synology metadata directories | Add `ExcludedDirectoryNames` option with @eaDir, @SynoResource, #recycle, @tmp |

## References

- [MassTransit Documentation](https://masstransit.io/documentation/concepts)
- [RabbitMQ Exchange Types](https://www.rabbitmq.com/tutorials/amqp-concepts.html#exchanges)
- [PostgreSQL DateTime Handling in .NET](https://www.npgsql.org/doc/types/datetime.html)
- [OpenTelemetry with MassTransit](https://masstransit.io/documentation/configuration/observability)
