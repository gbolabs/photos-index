# 001: API Client Alignment

**Status**: 🔲 Not Started
**Priority**: P1
**Agent**: A2 (IndexingService)
**Estimated Effort**: Medium

## Objective

Fix misalignments between IndexingService API client and the actual API endpoints.

## Background

During deployment testing, several issues were discovered where the IndexingService client code doesn't match the API contracts:

1. **Wrong route paths**: `api/scandirectories` vs `api/scan-directories`
2. **Wrong response types**: Expecting `List<T>` when API returns `PagedResponse<T>`
3. **Wrong configuration key**: `ApiBaseUrl` vs `API_BASE_URL`
4. **Missing pagination handling**: Client doesn't handle paged responses properly

## Issues Found

| Issue | Location | Fix Applied |
|-------|----------|-------------|
| Route path mismatch | `PhotosApiClient.cs` | ✅ Fixed |
| PagedResponse handling | `PhotosApiClient.cs` | ✅ Fixed |
| Config key mismatch | `Program.cs` | ✅ Fixed |
| Database dependency | `IndexingService.csproj` | ✅ Fixed |

## Remaining Work

- [ ] Add proper pagination support (fetch all pages if needed)
- [ ] Add retry/resilience with Polly
- [ ] Add response caching for directories
- [ ] Improve error handling with structured errors
- [ ] Add health check endpoint call before operations
- [ ] Consider using shared API client library (Refit/NSwag)

## Acceptance Criteria

- [ ] All API calls use correct routes
- [ ] All response types match API contracts
- [ ] Pagination handled correctly
- [ ] Retry logic for transient failures
- [ ] Structured error responses

## Files to Review

| File | Purpose |
|------|---------|
| `src/IndexingService/ApiClient/PhotosApiClient.cs` | HTTP client |
| `src/IndexingService/ApiClient/IPhotosApiClient.cs` | Interface |
| `src/Api/Controllers/*.cs` | API endpoints |
| `src/Shared/Responses/*.cs` | Response types |

## Test Verification

```bash
# Verify API routes match
curl http://localhost:8080/api/scan-directories
curl http://localhost:8080/api/files
curl http://localhost:8080/api/duplicates

# Check response format
curl http://localhost:8080/api/scan-directories | jq '.items'
```
