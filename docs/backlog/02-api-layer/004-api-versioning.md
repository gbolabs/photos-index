# 004: API Versioning

**Status**: 🔲 Not Started
**Priority**: P1 (Next Release)
**Agent**: A1
**Branch**: `feature/api-versioning`
**Estimated Complexity**: Medium
**Target Release**: v0.2.0

## Objective

Implement API versioning to support independent deployment of services (Indexer, API, Web). This ensures backward compatibility when services are deployed at different versions.

## Motivation

The system deploys three main components independently:
- **Indexing Service**: Scans files and calls API to ingest data
- **API**: Serves data to Web and receives data from Indexer
- **Web**: Consumes API endpoints

When rolling updates happen, services may temporarily run different versions. API versioning prevents breaking changes from causing failures during deployments.

## Dependencies

- None (foundational change)

## Acceptance Criteria

- [ ] URL-based versioning: `/api/v1/files`, `/api/v1/scan-directories`, etc.
- [ ] Version header support: `api-version: 1.0` (optional)
- [ ] Default to latest stable version when not specified
- [ ] Swagger/OpenAPI documents each version separately
- [ ] Deprecation headers for sunset versions
- [ ] Version compatibility matrix documented
- [ ] Indexing Service uses explicit version in API calls
- [ ] Web app uses explicit version in API calls
- [ ] Integration tests verify cross-version compatibility

## Implementation Approach

### Option A: ASP.NET Core API Versioning (Recommended)
Use `Asp.Versioning.Http` NuGet package:

```csharp
// Program.cs
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("api-version")
    );
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Controllers
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/files")]
public class IndexedFilesController : ControllerBase
```

### Option B: Manual Route Prefixes
Simpler but less flexible - just prefix all routes with `/api/v1/`.

## Files to Modify

- `src/Api/Program.cs` - Add versioning services
- `src/Api/Controllers/*.cs` - Add version attributes and update routes
- `src/Shared/ApiRoutes.cs` - Centralize route constants (new file)
- `src/IndexingService/Services/ApiClient.cs` - Use versioned endpoints
- `src/Web/src/app/core/api.service.ts` - Use versioned endpoints
- `Directory.Packages.props` - Add Asp.Versioning.Http package

## Version Strategy

| Version | Status | Support Until |
|---------|--------|---------------|
| v1 | Current | TBD |

New versions created only when breaking changes are required. Minor additions (new endpoints, new optional fields) don't require new versions.

## Test Coverage

- Unit tests for version routing
- Integration tests for version negotiation
- Cross-version compatibility tests (v1 client → v1 server)

## Success Criteria

- [ ] Indexer v0.1.x works with API v0.2.x (via v1 endpoints)
- [ ] Web v0.1.x works with API v0.2.x (via v1 endpoints)
- [ ] Zero downtime during rolling deployments
- [ ] Clear documentation of version compatibility
