# 004: Fix Aspire Dashboard Connectivity

**Status**: 🔲 Not Started
**Priority**: P1
**Agent**: A4 (Infrastructure)
**Branch**: `feature/aspire-healthcheck`
**Estimated Effort**: Small

## Objective

Add health check to Aspire Dashboard container and configure proper service startup ordering to ensure telemetry collectors are ready before application services start sending data.

## Background

Services may start before Aspire Dashboard is ready to receive telemetry, causing early spans to be lost. By adding a health check and updating `depends_on` conditions, we ensure reliable telemetry collection from startup.

## Dependencies

- None (infrastructure change)

## Acceptance Criteria

- [ ] Aspire Dashboard has working health check
- [ ] API waits for Aspire to be healthy before starting
- [ ] Indexing service waits for Aspire to be healthy
- [ ] No lost telemetry on startup
- [ ] Health check visible in `docker compose ps`

## Implementation

### 1. Aspire Dashboard Health Check

The Aspire Dashboard image exposes a UI on port 18888. We can health check this endpoint:

```yaml
aspire-dashboard:
  image: mcr.microsoft.com/dotnet/aspire-dashboard:9.1
  healthcheck:
    test: ["CMD-SHELL", "wget -q --spider http://localhost:18888/ || exit 1"]
    interval: 10s
    timeout: 5s
    retries: 5
    start_period: 10s
```

Note: Using wget instead of curl as the Aspire image is based on Alpine and may have wget available.

### 2. Update Service Dependencies

```yaml
api:
  depends_on:
    postgres:
      condition: service_healthy
    aspire-dashboard:
      condition: service_healthy

indexing-service:
  depends_on:
    api:
      condition: service_healthy
    postgres:
      condition: service_healthy
    aspire-dashboard:
      condition: service_healthy

cleaner-service:
  depends_on:
    api:
      condition: service_healthy
    postgres:
      condition: service_healthy
    aspire-dashboard:
      condition: service_healthy
```

### 3. OpenTelemetry Retry Configuration (Optional)

If startup ordering isn't enough, configure OTLP exporter retries:

```csharp
// src/Shared/Extensions/OpenTelemetryExtensions.cs
.AddOtlpExporter(o =>
{
    o.Endpoint = new Uri(endpoint);
    o.Protocol = OtlpExportProtocol.Grpc;
    o.TimeoutMilliseconds = 10000;
    // Retry configuration
    o.ExportProcessorType = ExportProcessorType.Batch;
    o.BatchExportProcessorOptions = new BatchExportProcessorOptions<Activity>
    {
        MaxQueueSize = 2048,
        ScheduledDelayMilliseconds = 5000,
        MaxExportBatchSize = 512
    };
})
```

## Files to Modify

| File | Changes |
|------|---------|
| `deploy/docker/docker-compose.yml` | Add Aspire health check, update depends_on |
| `deploy/kubernetes/photos-index.yaml` | Add init container or startup probe (optional) |
| `src/Shared/Extensions/OpenTelemetryExtensions.cs` | Add batch/retry config (optional) |

## Test Verification

```bash
# Start services
docker compose up -d

# Check health status
docker compose ps
# Should show aspire-dashboard as "healthy"

# Verify API waited for Aspire
docker compose logs api | grep -i "connected\|telemetry"

# Open Aspire Dashboard
# http://localhost:18888 - should show all services from startup
```

## Completion Checklist

- [ ] Health check added to Aspire Dashboard
- [ ] Service dependencies updated
- [ ] Tested with fresh `docker compose up`
- [ ] Verified all services appear in Aspire from startup
- [ ] PR created and linked above
- [ ] Status updated to Complete
