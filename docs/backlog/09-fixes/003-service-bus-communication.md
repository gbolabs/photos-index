# 003: Service Bus Communication

**Status**: 🔲 Not Started
**Priority**: P3
**Agent**: A2/A3
**Estimated Effort**: Large

## Objective

Replace direct HTTP communication between services with asynchronous message-based communication using a service bus/message queue.

## Background

Currently, the IndexingService communicates with the API via HTTP:
- `PhotosApiClient.cs` makes HTTP calls to ingest files
- Tight coupling between services
- Synchronous communication can cause bottlenecks
- Service failures cascade (if API is down, indexer fails)

## Benefits of Service Bus

| Benefit | Description |
|---------|-------------|
| **Decoupling** | Services don't need to know about each other |
| **Resilience** | Messages persist if consumer is down |
| **Scalability** | Multiple consumers can process in parallel |
| **Backpressure** | Queue absorbs load spikes |
| **Observability** | Queue depth = visibility into lag |

## Technology Options

### Option A: RabbitMQ (Recommended for self-hosted)
- Lightweight, easy to deploy on Synology NAS
- AMQP protocol, well-supported in .NET
- Docker image available
- MassTransit or raw RabbitMQ.Client

### Option B: Azure Service Bus
- Managed service, no infrastructure
- Good for cloud deployment
- Higher cost

### Option C: Redis Streams
- Already familiar if using Redis for caching
- Simpler than RabbitMQ
- Less feature-rich

### Option D: PostgreSQL LISTEN/NOTIFY + Queue Table
- No additional infrastructure
- Use existing database
- Limited scalability

## Proposed Architecture

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│ IndexingService │────▶│   RabbitMQ      │────▶│   API Worker    │
│                 │     │                 │     │                 │
│ Publishes:      │     │ Queues:         │     │ Consumes:       │
│ - FileDiscovered│     │ - file.discovered│    │ - FileDiscovered│
│ - FileHashed    │     │ - file.hashed   │     │ - FileHashed    │
│ - ScanComplete  │     │ - scan.complete │     │ - ScanComplete  │
└─────────────────┘     └─────────────────┘     └─────────────────┘

┌─────────────────┐                            ┌─────────────────┐
│ CleanerService  │────────────────────────────│   API Worker    │
│                 │                            │                 │
│ Publishes:      │     Queues:                │ Consumes:       │
│ - FileDeleted   │     - file.deleted         │ - FileDeleted   │
│ - CleanupDone   │     - cleanup.complete     │ - CleanupDone   │
└─────────────────┘                            └─────────────────┘
```

## Message Types

```csharp
// Messages/FileDiscoveredMessage.cs
public record FileDiscoveredMessage
{
    public Guid ScanDirectoryId { get; init; }
    public string FilePath { get; init; }
    public long FileSize { get; init; }
    public DateTime ModifiedAt { get; init; }
}

// Messages/FileHashedMessage.cs
public record FileHashedMessage
{
    public Guid ScanDirectoryId { get; init; }
    public string FilePath { get; init; }
    public string Sha256Hash { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

// Messages/ScanCompleteMessage.cs
public record ScanCompleteMessage
{
    public Guid ScanDirectoryId { get; init; }
    public int FilesProcessed { get; init; }
    public int FilesAdded { get; init; }
    public int FilesUpdated { get; init; }
    public int FilesFailed { get; init; }
    public TimeSpan Duration { get; init; }
}
```

## Implementation Steps

### Phase 1: Infrastructure
1. Add RabbitMQ to docker-compose.yml and kubernetes manifest
2. Add MassTransit NuGet packages
3. Create Shared.Messages project for message contracts

### Phase 2: Publisher (IndexingService)
1. Replace `IPhotosApiClient` with `IMessagePublisher`
2. Publish `FileDiscoveredMessage` when file found
3. Publish `FileHashedMessage` after hash computed
4. Publish `ScanCompleteMessage` when scan finishes

### Phase 3: Consumer (API)
1. Add background worker to consume messages
2. Process `FileDiscoveredMessage` → create/update IndexedFile
3. Process `FileHashedMessage` → update hash, detect duplicates
4. Process `ScanCompleteMessage` → update ScanDirectory.LastScannedAt

### Phase 4: CleanerService
1. Publish `FileDeletedMessage` when file removed
2. API consumer updates database

## Docker Compose Addition

```yaml
rabbitmq:
  image: rabbitmq:3-management-alpine
  container_name: photos-index-rabbitmq
  ports:
    - "5672:5672"   # AMQP
    - "15672:15672" # Management UI
  environment:
    RABBITMQ_DEFAULT_USER: photos
    RABBITMQ_DEFAULT_PASS: photos
  volumes:
    - rabbitmq_data:/var/lib/rabbitmq
  healthcheck:
    test: rabbitmq-diagnostics -q ping
    interval: 10s
    timeout: 5s
    retries: 5
```

## NuGet Packages

```xml
<!-- MassTransit with RabbitMQ -->
<PackageReference Include="MassTransit" />
<PackageReference Include="MassTransit.RabbitMQ" />

<!-- Or raw client -->
<PackageReference Include="RabbitMQ.Client" />
```

## Acceptance Criteria

- [ ] RabbitMQ running in docker-compose and kubernetes
- [ ] IndexingService publishes messages instead of HTTP calls
- [ ] API consumes messages and updates database
- [ ] Messages visible in RabbitMQ management UI
- [ ] Graceful handling when RabbitMQ is unavailable
- [ ] Dead letter queue for failed messages
- [ ] OpenTelemetry tracing through message flow

## Migration Strategy

1. Keep HTTP client as fallback initially
2. Add feature flag: `USE_SERVICE_BUS=true`
3. Run both in parallel, compare results
4. Remove HTTP client after validation

## Observability

- RabbitMQ metrics exported to Aspire Dashboard
- Message processing duration traced
- Queue depth monitored
- Dead letter queue alerts

## References

- [MassTransit Documentation](https://masstransit.io/)
- [RabbitMQ .NET Tutorial](https://www.rabbitmq.com/tutorials/tutorial-one-dotnet.html)
- [Outbox Pattern](https://microservices.io/patterns/data/transactional-outbox.html)
