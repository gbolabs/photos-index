# 009: Async Best Practices

**Status**: 🔲 Not Started
**Priority**: P3 (Low Priority)
**Agent**: A1/A2
**Branch**: `feature/code-quality-async-patterns`
**Estimated Complexity**: Low

## Objective

Apply async/await best practices including `ConfigureAwait(false)` for library code and `ValueTask<T>` for hot paths.

## Dependencies

None

## Problem Statement

Current async code is good but could be optimized:
- No `ConfigureAwait(false)` in library/service code
- Could use `ValueTask<T>` for frequently-called methods
- All cancellation tokens are properly passed (good!)

## Acceptance Criteria

- [ ] Add `ConfigureAwait(false)` to all library code (Services, Database)
- [ ] Keep default behavior in Controllers (need sync context)
- [ ] Consider `ValueTask<T>` for hot path methods
- [ ] Ensure all changes don't break functionality
- [ ] All tests still pass

## Implementation Plan

### 1. ConfigureAwait(false) Pattern

**Apply to Service Layer:**

Before:
```csharp
public async Task<IndexedFileDto?> GetByIdAsync(Guid id, CancellationToken ct)
{
    var entity = await _dbContext.IndexedFiles
        .AsNoTracking()
        .FirstOrDefaultAsync(f => f.Id == id, ct);
        
    return entity is null ? null : MapToDto(entity);
}
```

After:
```csharp
public async Task<IndexedFileDto?> GetByIdAsync(Guid id, CancellationToken ct)
{
    var entity = await _dbContext.IndexedFiles
        .AsNoTracking()
        .FirstOrDefaultAsync(f => f.Id == id, ct)
        .ConfigureAwait(false);  // No sync context needed
        
    return entity is null ? null : MapToDto(entity);
}
```

### 2. Where to Apply ConfigureAwait(false)

**DO use in:**
- Service implementations (`src/Api/Services/`)
- Database layer (`src/Database/`)
- IndexingService (`src/IndexingService/Services/`)
- CleanerService (`src/CleanerService/`)

**DON'T use in:**
- Controllers (need sync context for HttpContext)
- Background services that use ExecutionContext
- Any code that accesses HttpContext after await

### 3. ValueTask<T> for Hot Paths

For methods called frequently with cached results:

```csharp
// Before
public async Task<bool> PathExistsAsync(string path, CancellationToken ct)
{
    if (_cache.TryGetValue(path, out bool exists))
        return exists;
        
    exists = await CheckPathAsync(path, ct);
    _cache.Set(path, exists);
    return exists;
}

// After
public async ValueTask<bool> PathExistsAsync(string path, CancellationToken ct)
{
    if (_cache.TryGetValue(path, out bool exists))
        return exists;  // Synchronous return, no Task allocation
        
    exists = await CheckPathAsync(path, ct).ConfigureAwait(false);
    _cache.Set(path, exists);
    return exists;
}
```

**ValueTask<T> is beneficial when:**
- Method often returns synchronously (from cache)
- Method is called very frequently
- Want to avoid Task allocation overhead

**Don't use ValueTask<T> if:**
- Method always awaits
- Result is awaited multiple times
- Need to use `.Result` or `.GetAwaiter().GetResult()`

### 4. Pattern Examples

**Service with ConfigureAwait:**
```csharp
public sealed class IndexedFileService : IIndexedFileService
{
    public async Task<PagedResponse<IndexedFileDto>> QueryAsync(
        FileQueryParameters query, 
        CancellationToken ct)
    {
        var totalItems = await _dbContext.IndexedFiles
            .CountAsync(ct)
            .ConfigureAwait(false);
            
        var items = await _dbContext.IndexedFiles
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);
            
        return new PagedResponse<IndexedFileDto>
        {
            Items = items.Select(MapToDto).ToList(),
            TotalItems = totalItems
        };
    }
}
```

**Controller without ConfigureAwait:**
```csharp
public class IndexedFilesController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<IndexedFileDto>>> Query(
        [FromQuery] FileQueryParameters query,
        CancellationToken ct = default)
    {
        // No ConfigureAwait - need HttpContext sync context
        var result = await _service.QueryAsync(query, ct);
        return Ok(result);
    }
}
```

## Files to Modify

```
src/Api/Services/
├── IndexedFileService.cs
├── DuplicateService.cs
├── ScanDirectoryService.cs
├── OriginalSelectionService.cs
└── (all other services)

src/IndexingService/Services/
├── FileScanner.cs
├── HashComputer.cs
├── MetadataExtractor.cs
└── IndexingOrchestrator.cs

src/Database/
└── (any async methods in repositories if added)

src/IndexingService/ApiClient/
└── PhotosApiClient.cs
```

## Performance Impact

Expected improvements:
- **ConfigureAwait(false)**: Avoids sync context capture/restore overhead
- **ValueTask<T>**: Reduces heap allocations for cached results
- **Combined**: 5-15% improvement on high-throughput scenarios

## Testing

Verify behavior unchanged:
```csharp
[Fact]
public async Task ServiceMethod_WithConfigureAwait_ReturnsCorrectResult()
{
    // Verify functionality unchanged
    var result = await _service.GetByIdAsync(id, CancellationToken.None);
    result.Should().NotBeNull();
}
```

## Benefits

- **Performance**: Reduced overhead for async calls
- **Scalability**: Better thread pool utilization
- **Best Practice**: Follows .NET async guidelines

## Risks

- **Deadlocks**: If misused in sync-over-async scenarios
- **Context Loss**: Must not access context-dependent data after ConfigureAwait(false)

## Guidelines

Document in CONTRIBUTING.md:

### When to use ConfigureAwait(false)

✅ Use in:
- Library code (services, utilities)
- Database operations
- File I/O operations
- Network calls
- Any code that doesn't need synchronization context

❌ Don't use in:
- Controllers
- Razor Pages
- Code that accesses HttpContext after await
- Background services using ExecutionContext

### When to use ValueTask<T>

✅ Use when:
- Method often returns synchronously
- Method is a hot path (called frequently)
- Want to optimize allocations

❌ Don't use when:
- Method always awaits
- Result awaited multiple times
- Complexity doesn't justify benefit

## Related Tasks

- `13-code-quality/006-ef-optimizations.md` - Performance improvements
- `05-performance/*` - General performance work

## References

- [ConfigureAwait FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/)
- [Understanding ValueTask](https://devblogs.microsoft.com/dotnet/understanding-the-whys-whats-and-whens-of-valuetask/)

## Completion Checklist

- [ ] Add ConfigureAwait(false) to all service methods
- [ ] Add ConfigureAwait(false) to IndexingService
- [ ] Add ConfigureAwait(false) to database operations
- [ ] Add ConfigureAwait(false) to API client
- [ ] Verify Controllers don't use ConfigureAwait
- [ ] Identify ValueTask<T> candidates
- [ ] Implement ValueTask<T> for hot paths
- [ ] Run all tests and verify passing
- [ ] Document guidelines in CONTRIBUTING.md
- [ ] PR created and reviewed
