# ADR 006: Jaeger for Distributed Tracing Over Aspire Dashboard

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

Replace Aspire Dashboard with Jaeger (`jaegertracing/all-in-one`) as the distributed tracing backend.

## Rationale

### Why Jaeger?

1. **Battle-tested at scale**: Jaeger was designed by Uber for high-volume distributed tracing
2. **Efficient ingestion**: OTLP collector handles high throughput without performance degradation
3. **Lightweight**: Single container with minimal resource requirements
4. **Native OTLP support**: Built-in OpenTelemetry Protocol support (gRPC on port 4317)
5. **Good UI**: Service dependency graphs, trace comparison, search functionality

### Trade-offs

| Aspect | Aspire Dashboard | Jaeger |
|--------|-----------------|--------|
| Logs | Integrated | Not included (use separate solution) |
| Metrics | Integrated | Not included (use Prometheus) |
| Traces | Yes | Yes (primary focus) |
| .NET Integration | Excellent | Standard OTLP |
| High-volume handling | Poor | Excellent |
| Resource usage | High under load | Low |

### What We Lose

- **Unified log view**: Aspire showed logs alongside traces. With Jaeger, logs are only in container stdout/Docker logs.
- **Metrics visualization**: Aspire displayed .NET metrics. Would need Prometheus + Grafana for metrics.

### What We Gain

- **Stability**: Jaeger handles the telemetry load without issues
- **Reliability**: Services no longer fail due to observability backend issues
- **Performance**: Lower CPU and memory usage on the host

## Consequences

### Immediate Changes

1. Replace `mcr.microsoft.com/dotnet/aspire-dashboard:9.1` with `jaegertracing/all-in-one:1.54`
2. Change OTLP endpoint from port `18889` to `4317`
3. Update Jaeger UI port from `18888` to `16686`
4. Remove health checks on tracing backend (use `service_started` instead of `service_healthy`)

### Configuration

```yaml
# Docker Compose
jaeger:
  image: jaegertracing/all-in-one:1.54
  ports:
    - '16686:16686'  # UI
    - '4317:4317'    # OTLP gRPC
  environment:
    COLLECTOR_OTLP_ENABLED: 'true'

# Services
OTEL_EXPORTER_OTLP_ENDPOINT: http://jaeger:4317
```

### Future Considerations

If we need logs and metrics visualization in the future:

1. **Logs**: Add Loki + Grafana or use Docker logging drivers
2. **Metrics**: Add Prometheus + Grafana
3. **Full stack**: Consider Grafana LGTM stack (Loki, Grafana, Tempo, Mimir)

## References

- [Jaeger Documentation](https://www.jaegertracing.io/docs/)
- [OpenTelemetry OTLP Specification](https://opentelemetry.io/docs/specs/otlp/)
- [Aspire Dashboard GitHub Issue on Performance](https://github.com/dotnet/aspire/issues)
