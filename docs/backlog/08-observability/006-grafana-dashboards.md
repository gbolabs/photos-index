# Grafana Dashboards for Distributed Processing

**Status**: 🔲 Not Started
**Priority**: Medium
**Track**: Observability

## Overview

Set up Grafana dashboards to monitor the distributed photo processing pipeline. Currently Grafana has Loki (logs) and Jaeger (traces) configured but no dashboards.

## Goals

1. Visualize processing throughput and latency
2. Monitor queue depths and backlogs
3. Track error rates by service
4. Enable log exploration with trace correlation

## Implementation

### Phase 1: Logs Dashboard
- Container log streams (API, MetadataService, ThumbnailService, Indexer)
- Error/warning counts over time
- Log search with filters

### Phase 2: Metrics (requires Prometheus)
- Add Prometheus to docker-compose
- Configure RabbitMQ Prometheus exporter
- .NET OpenTelemetry metrics exporter
- MinIO metrics

### Phase 3: Dashboard Panels
| Panel | Source | Description |
|-------|--------|-------------|
| Files/minute | Loki (log parsing) | Processing throughput |
| Processing latency | Jaeger | P50/P95/P99 by service |
| Queue depth | Prometheus/RabbitMQ | Messages waiting |
| Error rate | Loki | Errors per service |
| Storage usage | Prometheus/MinIO | Bucket sizes |
| Active traces | Jaeger | Request flow visualization |

### Phase 4: Alerts (optional)
- Queue depth > threshold
- Error rate spike
- Processing latency degradation

## Files to Modify

| File | Changes |
|------|---------|
| `deploy/docker/docker-compose.yml` | Add Prometheus service |
| `deploy/docker/prometheus.yml` | Scrape configs |
| `deploy/docker/grafana/dashboards/` | Dashboard JSON files |
| TrueNAS YAML | Same additions |

## Dependencies

- Prometheus (new)
- RabbitMQ Prometheus plugin (already enabled)
- .NET OpenTelemetry metrics

## Acceptance Criteria

- [ ] Grafana dashboard shows real-time processing stats
- [ ] Can drill down from dashboard to logs to traces
- [ ] Queue depth visible
- [ ] Error rate tracking works

## Notes

Current stack: Grafana + Loki + Jaeger (traces + logs)
Missing: Prometheus (metrics)
