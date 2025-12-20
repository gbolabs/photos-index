# 003: Traefik OpenTelemetry Integration

**Status**: 🔲 Not Started
**Priority**: P1
**Agent**: A4 (Infrastructure)
**Branch**: `feature/traefik-otel`
**Estimated Effort**: Small

## Objective

Configure Traefik to send traces to Aspire Dashboard via OpenTelemetry OTLP protocol.

## Background

Traefik v3.x supports native OpenTelemetry tracing. By enabling this, all HTTP requests through Traefik will appear in Aspire Dashboard, providing end-to-end visibility from ingress to backend services.

## Dependencies

- 08-004 (Aspire Health Check) - recommended to ensure Aspire is ready before Traefik sends traces

## Acceptance Criteria

- [ ] Traefik appears as `photos-index-traefik` in Aspire Dashboard
- [ ] Traces show full request path: Traefik -> API/Web
- [ ] Trace context propagated via W3C traceparent header
- [ ] Sample rate configurable via environment variable

## Implementation

### 1. Docker Compose - Traefik Args

Add OTLP configuration to Traefik service:

```yaml
traefik:
  command:
    # ... existing args ...
    # OpenTelemetry tracing
    - "--tracing=true"
    - "--tracing.otlp=true"
    - "--tracing.otlp.grpc.endpoint=aspire-dashboard:18889"
    - "--tracing.otlp.grpc.insecure=true"
    - "--tracing.serviceName=photos-index-traefik"
    - "--tracing.sampleRate=${TRAEFIK_TRACE_SAMPLE_RATE:-1.0}"
```

### 2. Kubernetes - Traefik Static Config

Update traefik.yml in ConfigMap:

```yaml
traefik.yml: |
  # ... existing config ...
  tracing:
    serviceName: photos-index-traefik
    sampleRate: 1.0
    otlp:
      grpc:
        endpoint: "localhost:18889"
        insecure: true
```

### 3. Environment Variable (.env.example)

```bash
# Traefik OTEL sample rate (0.0 to 1.0)
# 1.0 = trace all requests, 0.1 = trace 10% of requests
TRAEFIK_TRACE_SAMPLE_RATE=1.0
```

## Files to Modify

| File | Changes |
|------|---------|
| `deploy/docker/docker-compose.yml` | Add OTLP tracing args to Traefik service |
| `deploy/kubernetes/photos-index.yaml` | Add tracing section to traefik.yml ConfigMap |
| `deploy/docker/.env.example` | Add TRAEFIK_TRACE_SAMPLE_RATE |

## Traefik OTLP Documentation

Reference: https://doc.traefik.io/traefik/observability/tracing/opentelemetry/

Key points:
- Traefik v3.0+ required for OTLP support
- Uses gRPC by default, HTTP also supported
- W3C trace context headers automatically propagated
- Service name appears in Aspire as configured

## Test Verification

```bash
# Make request through Traefik
curl http://localhost:8080/api/health

# Open Aspire Dashboard at http://localhost:18888
# Should see trace with:
# - photos-index-traefik (ingress span)
# - photos-index-api (backend span)
```

## Completion Checklist

- [ ] Docker Compose updated
- [ ] Kubernetes manifest updated
- [ ] .env.example updated
- [ ] Tested with Docker Compose
- [ ] Verified traces in Aspire
- [ ] PR created and linked above
- [ ] Status updated to Complete
