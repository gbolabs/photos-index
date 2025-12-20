# 001: Enable Swagger in Production

**Status**: ✅ Complete
**PR**: [#14](https://github.com/gbolabs/photos-index/pull/14)
**Priority**: P1
**Agent**: A1 (API)
**Branch**: `feature/observability-enhancements`
**Estimated Effort**: Small
**Completed**: 2025-12-20

## Objective

Enable Swagger UI unconditionally (not just in Development) so API documentation is accessible via Traefik at `/api/swagger`.

## Background

Currently, Swagger is only enabled when `ASPNETCORE_ENVIRONMENT=Development`. In containerized deployments, we typically run with `Production`, which hides the API documentation. For internal APIs like this, having Swagger accessible improves developer experience.

## Dependencies

- None (standalone change)

## Acceptance Criteria

- [ ] Swagger UI accessible at `http://localhost:8080/api/swagger`
- [ ] Swagger JSON at `http://localhost:8080/api/swagger/v1/swagger.json`
- [ ] Works in both Development and Production environments
- [ ] Traefik routes `/api/swagger/*` without stripping prefix

## Implementation

### 1. API Program.cs Changes

Remove the `IsDevelopment()` check and configure proper base path:

```csharp
// Enable Swagger in all environments
app.UseSwagger(c =>
{
    c.RouteTemplate = "swagger/{documentName}/swagger.json";
});

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/api/swagger/v1/swagger.json", "Photos Index API v1");
    c.RoutePrefix = "swagger";
});
```

### 2. Traefik Routing (docker-compose.yml)

Add a dedicated Swagger router with higher priority that doesn't strip the prefix:

```yaml
labels:
  # Swagger router - no prefix stripping
  - "traefik.http.routers.api-swagger.rule=PathPrefix(`/api/swagger`)"
  - "traefik.http.routers.api-swagger.entrypoints=web"
  - "traefik.http.routers.api-swagger.priority=110"
  - "traefik.http.routers.api-swagger.service=api"
```

### 3. Kubernetes photos-index.yaml

Add equivalent Swagger route in Traefik dynamic config:

```yaml
api-swagger-router:
  rule: "PathPrefix(`/api/swagger`)"
  service: api-service
  priority: 110
```

## Files to Modify

| File | Changes |
|------|---------|
| `src/Api/Program.cs` | Remove environment check, configure Swagger base path |
| `deploy/docker/docker-compose.yml` | Add Swagger Traefik router |
| `deploy/kubernetes/photos-index.yaml` | Add Swagger route to Traefik config |

## Test Verification

```bash
# After deployment
curl -s http://localhost:8080/api/swagger/index.html | grep -q "swagger-ui" && echo "OK"
curl -s http://localhost:8080/api/swagger/v1/swagger.json | jq .info.title
```

## Completion Checklist

- [ ] Code changes complete
- [ ] Tested locally with `dotnet run`
- [ ] Tested with Docker Compose
- [ ] PR created and linked above
- [ ] Status updated to Complete
