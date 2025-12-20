# 002: API TraceId in Responses

**Status**: ✅ Complete
**PR**: [#14](https://github.com/gbolabs/photos-index/pull/14)
**Priority**: P1
**Agent**: A1 (API)
**Branch**: `feature/observability-enhancements`
**Estimated Effort**: Medium
**Completed**: 2025-12-20

## Objective

Add `X-Trace-Id` header to all API responses and include trace ID in error responses for easier debugging and correlation with Aspire Dashboard.

## Background

When troubleshooting issues, users need to correlate API responses with telemetry data in Aspire Dashboard. By including the OpenTelemetry trace ID in response headers and error bodies, users can search for the exact trace in the dashboard.

## Dependencies

- 08-001 (Swagger in Production) - recommended to complete first for easier testing

## Acceptance Criteria

- [ ] Every API response includes `X-Trace-Id` header
- [ ] Error responses include `traceId` field in JSON body
- [ ] Trace ID matches what appears in Aspire Dashboard
- [ ] Works with both success and error responses

## Implementation

### 1. Create TraceIdMiddleware

```csharp
// src/Api/Middleware/TraceIdMiddleware.cs
public class TraceIdMiddleware
{
    private readonly RequestDelegate _next;

    public TraceIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        context.Response.Headers["X-Trace-Id"] = traceId;
        await _next(context);
    }
}
```

### 2. Update ApiErrorResponse

```csharp
// src/Shared/Responses/ApiErrorResponse.cs
public class ApiErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? TraceId { get; set; }

    public static ApiErrorResponse NotFound(string message, string? traceId = null)
        => new() { Message = message, Code = "NOT_FOUND", TraceId = traceId };

    public static ApiErrorResponse BadRequest(string message, string? traceId = null)
        => new() { Message = message, Code = "BAD_REQUEST", TraceId = traceId };

    // ... other factory methods
}
```

### 3. Create BaseApiController

```csharp
// src/Api/Controllers/BaseApiController.cs
[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected string CurrentTraceId =>
        Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;

    protected ActionResult NotFoundWithTrace(string message)
        => NotFound(ApiErrorResponse.NotFound(message, CurrentTraceId));

    protected ActionResult BadRequestWithTrace(string message)
        => BadRequest(ApiErrorResponse.BadRequest(message, CurrentTraceId));
}
```

### 4. Register Middleware in Program.cs

```csharp
// Early in pipeline, before other middleware
app.UseMiddleware<TraceIdMiddleware>();
```

## Files to Create

| File | Purpose |
|------|---------|
| `src/Api/Middleware/TraceIdMiddleware.cs` | Adds X-Trace-Id header to all responses |
| `src/Api/Controllers/BaseApiController.cs` | Helper methods for controllers |

## Files to Modify

| File | Changes |
|------|---------|
| `src/Shared/Responses/ApiErrorResponse.cs` | Add TraceId property and update factory methods |
| `src/Api/Program.cs` | Register TraceIdMiddleware |
| `src/Api/Controllers/*.cs` | Inherit from BaseApiController (optional) |

## Test Verification

```bash
# Check header on any request
curl -I http://localhost:8080/api/health | grep "X-Trace-Id"

# Check error response includes traceId
curl http://localhost:8080/api/indexed-files/00000000-0000-0000-0000-000000000000 | jq .traceId
```

## Completion Checklist

- [ ] TraceIdMiddleware created
- [ ] ApiErrorResponse updated with TraceId
- [ ] BaseApiController created
- [ ] Unit tests for middleware
- [ ] Integration test verifying header presence
- [ ] PR created and linked above
- [ ] Status updated to Complete
