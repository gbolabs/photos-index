# 005: Angular OpenTelemetry Integration

**Status**: 🔲 Not Started
**Priority**: P1
**Agent**: A4 (Web UI)
**Branch**: `feature/angular-otel`
**Estimated Effort**: Medium

## Objective

Add OpenTelemetry instrumentation to the Angular application to:
1. Initiate distributed traces from the browser
2. Propagate trace context to backend API via W3C traceparent header
3. Display trace IDs in error notifications for user debugging

## Background

For full end-to-end observability, traces should start at the user's browser. When Angular initiates an API request, it should create a span and propagate the trace context. This allows the entire request flow to be visible in Aspire Dashboard: Browser -> Traefik -> API -> Database.

## Dependencies

- 08-002 (API TraceId in Responses) - API must return X-Trace-Id header
- 08-003 (Traefik OTEL) - Traefik must propagate trace context

## Acceptance Criteria

- [ ] OpenTelemetry SDK initialized in Angular app
- [ ] HTTP interceptor creates span for each API request
- [ ] W3C traceparent header added to outgoing requests
- [ ] Error notifications show trace ID (first 8 chars)
- [ ] Traces visible in Aspire Dashboard with Angular as root
- [ ] Optional OTLP exporter for browser-side traces

## Implementation

### 1. OpenTelemetry Packages

```json
{
  "dependencies": {
    "@opentelemetry/api": "^1.9.0",
    "@opentelemetry/context-zone": "^1.30.1",
    "@opentelemetry/exporter-trace-otlp-http": "^0.57.2",
    "@opentelemetry/instrumentation": "^0.57.2",
    "@opentelemetry/resources": "^1.30.1",
    "@opentelemetry/sdk-trace-base": "^1.30.1",
    "@opentelemetry/sdk-trace-web": "^1.30.1",
    "@opentelemetry/semantic-conventions": "^1.28.0"
  }
}
```

### 2. TelemetryService

Create `src/app/core/telemetry.service.ts`:
- Initialize WebTracerProvider with ZoneContextManager
- Configure OTLP exporter (optional, endpoint from runtime config)
- Provide helper methods: startSpan, getCurrentTraceId, getTraceParentHeader

### 3. TelemetryInterceptor

Create `src/app/core/telemetry.interceptor.ts`:
- Implement HttpInterceptor
- Create span for each request with attributes: method, url, host
- Add traceparent header to request
- Record response status and duration
- Extract X-Trace-Id from response for correlation
- Set span status based on response

### 4. App Configuration

Update `src/app/app.config.ts`:
- Register APP_INITIALIZER to initialize TelemetryService
- Register HTTP_INTERCEPTORS with TelemetryInterceptor

### 5. Error Notification Enhancement

Update notification service to show trace ID:
```typescript
error(message: string, duration = 5000, traceId?: string): void {
  const displayMessage = traceId
    ? `${message} (Trace: ${traceId.substring(0, 8)}...)`
    : message;
  // ...
}
```

## Files to Create

| File | Purpose |
|------|---------|
| `src/app/core/telemetry.service.ts` | OpenTelemetry SDK initialization |
| `src/app/core/telemetry.interceptor.ts` | HTTP request instrumentation |

## Files to Modify

| File | Changes |
|------|---------|
| `package.json` | Add OpenTelemetry packages |
| `src/app/app.config.ts` | Register telemetry provider and interceptor |
| `src/app/services/notification.service.ts` | Add traceId parameter to error() |
| `src/app/services/api-error-handler.ts` | Pass traceId to notification |

## Runtime Configuration

The OTLP endpoint can be configured at runtime via window global:
```javascript
// Injected by nginx/docker at startup
window.__OTEL_ENDPOINT__ = 'http://localhost:4318/v1/traces';
```

For containers, this can be done via env.sh script in nginx.

## Test Verification

```bash
# 1. Open browser dev tools -> Network tab
# 2. Make any API request
# 3. Verify 'traceparent' header is present in request

# 4. Open Aspire Dashboard: http://localhost:18888
# 5. Should see traces starting from 'photos-index-web'

# 6. Trigger an error (e.g., request non-existent file)
# 7. Error notification should show trace ID
```

## Security Considerations

- OTLP endpoint should only be configured for development/internal deployments
- In production, consider disabling browser-side trace export
- Trace IDs in error messages help debugging but don't expose sensitive data

## Completion Checklist

- [ ] OpenTelemetry packages added
- [ ] TelemetryService created and tested
- [ ] TelemetryInterceptor created and tested
- [ ] Error notifications show trace ID
- [ ] Traces visible in Aspire Dashboard
- [ ] Unit tests updated
- [ ] PR created and linked above
- [ ] Status updated to Complete
