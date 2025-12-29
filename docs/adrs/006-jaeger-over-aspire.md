# ADR 006: Grafana Observability Stack Over Aspire Dashboard

## Status

Accepted

## Context

The Photos Index application uses OpenTelemetry for observability. Initially, we used the .NET Aspire Dashboard as the telemetry backend because:

1. It's designed for .NET applications
2. Provides unified view of logs, traces, and metrics
3. Easy to set up with a single container

However, during production deployment on TrueNAS with the v0.3.0 distributed architecture (API, Indexing Service, Metadata Service, Thumbnail Service, Cleaner Service), we encountered severe performance issues:

- **High CPU usage**: Aspire Dashboard couldn't handle the volume of incoming telemetry
- **Network saturation**: Multiple services sending traces simultaneously overloaded the dashboard
- **Container instability**: The Aspire container became unresponsive under load
- **Health check failures**: Dependent services failed to start due to Aspire health check timeouts

## Decision

Replace Aspire Dashboard with a Grafana-based observability stack:

- **Jaeger** (`jaegertracing/all-in-one:1.54`) - Distributed tracing with persistent storage
- **Loki** (`grafana/loki:2.9.0`) - Log aggregation
- **Promtail** (`grafana/promtail:2.9.0`) - Log collector from Docker containers
- **Grafana** (`grafana/grafana:latest`) - Unified dashboard for logs and traces

## Rationale

### Why This Stack?

1. **Battle-tested at scale**: Jaeger (Uber) and Loki (Grafana Labs) handle high-volume telemetry
2. **Persistent storage**: All components use disk storage, not memory
3. **Unified UI**: Grafana provides single pane of glass for logs and traces
4. **Log-trace correlation**: Click from log line to related trace via TraceID
5. **Native OTLP support**: Jaeger accepts OpenTelemetry Protocol on port 4317
6. **Docker-native log collection**: Promtail auto-discovers containers

### Component Responsibilities

| Component | Purpose | Storage |
|-----------|---------|---------|
| Jaeger | Distributed traces | Badger (disk) |
| Loki | Log aggregation | Filesystem |
| Promtail | Collect Docker logs | N/A (stateless) |
| Grafana | Unified dashboards | SQLite |

### Comparison with Aspire

| Aspect | Aspire Dashboard | Grafana Stack |
|--------|-----------------|---------------|
| Logs | Integrated | Loki + Promtail |
| Traces | Yes | Jaeger |
| Metrics | Integrated | Prometheus (optional) |
| Persistence | Memory only | Disk-based |
| High-volume handling | Poor | Excellent |
| Resource usage | High under load | Moderate, stable |
| Log-trace correlation | Yes | Yes (via TraceID) |

### What We Gain

- **Stability**: Stack handles telemetry load without issues
- **Persistence**: Data survives container restarts
- **Scalability**: Each component can scale independently
- **Flexibility**: Add Prometheus for metrics later
- **Industry standard**: Well-documented, large community

### Trade-offs

- **More containers**: 4 containers vs 1 for Aspire
- **Configuration**: Requires Promtail and Grafana datasource setup
- **Disk usage**: Persistent storage requires disk space management

## Consequences

### Architecture

```
┌─────────────┐     ┌─────────────┐
│   Services  │────▶│   Jaeger    │──▶ Traces (Badger)
│  (OTLP)     │     │  :4317      │
└─────────────┘     └─────────────┘
                           │
┌─────────────┐     ┌──────▼──────┐
│  Promtail   │────▶│    Loki     │──▶ Logs (Filesystem)
│  (Docker)   │     │   :3100     │
└─────────────┘     └─────────────┘
                           │
                    ┌──────▼──────┐
                    │   Grafana   │──▶ Dashboards
                    │   :3000     │
                    └─────────────┘
```

### Ports

| Service | Port | Purpose |
|---------|------|---------|
| Jaeger | 4317 | OTLP gRPC ingestion |
| Jaeger | 16686 | Jaeger UI (optional) |
| Loki | 3100 | Log ingestion |
| Grafana | 3000 | Unified dashboard |

### Configuration

```yaml
# Jaeger with persistent Badger storage
jaeger:
  image: jaegertracing/all-in-one:1.54
  environment:
    COLLECTOR_OTLP_ENABLED: 'true'
    SPAN_STORAGE_TYPE: badger
    BADGER_EPHEMERAL: 'false'
    BADGER_DIRECTORY_VALUE: /badger/data
    BADGER_DIRECTORY_KEY: /badger/key
  volumes:
    - jaeger_data:/badger

# Loki with filesystem storage
loki:
  image: grafana/loki:2.9.0
  volumes:
    - loki_data:/loki

# Promtail collecting Docker logs
promtail:
  image: grafana/promtail:2.9.0
  volumes:
    - /var/run/docker.sock:/var/run/docker.sock:ro

# Grafana with pre-configured datasources
grafana:
  image: grafana/grafana:latest
  volumes:
    - grafana_data:/var/lib/grafana

# Services send traces to Jaeger
OTEL_EXPORTER_OTLP_ENDPOINT: http://jaeger:4317
```

### Grafana Datasource Configuration

Pre-provisioned datasources enable log-trace correlation:

```yaml
datasources:
  - name: Jaeger
    type: jaeger
    url: http://jaeger:16686
  - name: Loki
    type: loki
    url: http://loki:3100
    jsonData:
      derivedFields:
        - name: TraceID
          matcherRegex: '"traceId":"([a-f0-9]+)"'
          datasourceUid: jaeger
```

### Storage Requirements

Estimate based on retention and volume:
- **Jaeger**: ~1GB per million traces
- **Loki**: ~100MB per million log lines (compressed)
- **Grafana**: <100MB for dashboards

Configure retention policies to manage disk usage.

### Volume Permissions

The observability containers run as non-root users. When using host path mounts (e.g., on TrueNAS), set ownership before first run:

```bash
# Jaeger runs as user 10001
sudo chown -R 10001:10001 /path/to/jaeger-data

# Loki runs as user 10001
sudo chown -R 10001:10001 /path/to/loki-data

# Grafana runs as user 472
sudo chown -R 472:472 /path/to/grafana-data
```

Named Docker volumes (default in compose) handle permissions automatically.

### Future Considerations

1. **Metrics**: Add Prometheus + node-exporter for system metrics
2. **Alerting**: Configure Grafana alerts for error rates
3. **Retention**: Add cleanup jobs for old traces/logs
4. **Scaling**: Move to Grafana Cloud or dedicated clusters if needed

## References

- [Jaeger Documentation](https://www.jaegertracing.io/docs/)
- [Grafana Loki Documentation](https://grafana.com/docs/loki/latest/)
- [Promtail Configuration](https://grafana.com/docs/loki/latest/clients/promtail/)
- [OpenTelemetry OTLP Specification](https://opentelemetry.io/docs/specs/otlp/)
