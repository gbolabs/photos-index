# PostgreSQL OpenTelemetry Metrics

## Problem Statement

PostgreSQL server metrics (connections, cache hit ratio, replication lag, etc.) are not visible in Aspire Dashboard. Only EF Core query traces are captured.

## Current State

- EF Core instrumentation sends SQL query traces to Aspire
- No PostgreSQL server-level metrics
- No visibility into connection pool usage, slow queries at DB level

## Proposed Solution

Add OpenTelemetry Collector with PostgreSQL receiver to export metrics to Aspire.

## Architecture

```
┌─────────────┐     ┌──────────────────┐     ┌─────────────┐
│  PostgreSQL │────▶│  OTEL Collector  │────▶│   Aspire    │
│    :5432    │     │  (postgres rcvr) │     │   :18889    │
└─────────────┘     └──────────────────┘     └─────────────┘
```

## Implementation

### Docker Compose Addition

```yaml
otel-collector:
  image: otel/opentelemetry-collector-contrib:latest
  container_name: photos-index-otel-collector
  command: ["--config=/etc/otel-collector-config.yaml"]
  volumes:
    - ./otel-collector-config.yaml:/etc/otel-collector-config.yaml:ro
  depends_on:
    - postgres
```

### Collector Configuration

```yaml
# otel-collector-config.yaml
receivers:
  postgresql:
    endpoint: postgres:5432
    username: photosindex
    password: ${POSTGRES_PASSWORD}
    databases:
      - photosindex
    collection_interval: 30s
    tls:
      insecure: true

exporters:
  otlp:
    endpoint: aspire-dashboard:18889
    tls:
      insecure: true

service:
  pipelines:
    metrics:
      receivers: [postgresql]
      exporters: [otlp]
```

### Metrics Available

| Metric | Description |
|--------|-------------|
| `postgresql.backends` | Active connections |
| `postgresql.commits` | Transactions committed |
| `postgresql.rollbacks` | Transactions rolled back |
| `postgresql.rows` | Rows fetched/inserted/updated/deleted |
| `postgresql.blocks_read` | Disk blocks read |
| `postgresql.buffer_hit` | Buffer cache hits |
| `postgresql.table.size` | Table sizes |
| `postgresql.index.size` | Index sizes |
| `postgresql.db.size` | Database size |

## Alternative: postgres_exporter

If OTEL Collector is too heavy, use prometheus/postgres_exporter with OTEL Collector's prometheus receiver:

```yaml
postgres-exporter:
  image: prometheuscommunity/postgres-exporter
  environment:
    DATA_SOURCE_NAME: "postgresql://photosindex:${POSTGRES_PASSWORD}@postgres:5432/photosindex?sslmode=disable"
```

## Dependencies

- `otel/opentelemetry-collector-contrib` image (includes postgres receiver)
- PostgreSQL user with `pg_monitor` role for metrics access

## Priority

**Low** - Nice to have, EF Core traces provide query-level visibility

## Effort

2-4 hours
