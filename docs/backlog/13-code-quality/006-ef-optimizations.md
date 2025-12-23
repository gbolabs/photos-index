# 006: Entity Framework Optimizations

**Status**: 🔲 Not Started
**Priority**: P3 (Low Priority)
**Agent**: A1
**Branch**: `feature/code-quality-ef-optimizations`
**Estimated Complexity**: Medium

## Objective

Optimize Entity Framework Core queries for better performance using compiled queries, query splitting, and improved tracking strategies.

## Dependencies

None - but should measure before implementing

## Problem Statement

Current EF Core usage is functional but could be optimized:
- No compiled queries for hot paths
- Could use `AsNoTrackingWithIdentityResolution` instead of `AsNoTracking` in some cases
- Complex includes might benefit from query splitting
- No explicit indexes on frequently filtered columns

## Acceptance Criteria

- [ ] Identify frequently executed queries (profiling)
- [ ] Implement compiled queries for hot paths
- [ ] Add query splitting for complex includes
- [ ] Review and optimize tracking strategies
- [ ] Add database indexes where missing
- [ ] Measure performance improvements
- [ ] All tests still pass

## Implementation Plan

### 1. Profile Current Performance

Use Application Insights or dotnet-trace to identify:
- Most frequently executed queries
- Slowest queries
- N+1 query problems

### 2. Implement Compiled Queries

**Example - IndexedFileService.cs:**

Before:
```csharp
var entity = await _dbContext.IndexedFiles
    .AsNoTracking()
    .FirstOrDefaultAsync(f => f.Id == id, ct);
```

After:
```csharp
private static readonly Func<PhotosDbContext, Guid, CancellationToken, Task<IndexedFile?>> 
    GetByIdQuery = EF.CompileAsyncQuery(
        (PhotosDbContext db, Guid id, CancellationToken ct) =>
            db.IndexedFiles
                .AsNoTracking()
                .FirstOrDefault(f => f.Id == id));

public async Task<IndexedFileDto?> GetByIdAsync(Guid id, CancellationToken ct)
{
    var entity = await GetByIdQuery(_dbContext, id, ct);
    // ...
}
```

### 3. Use Query Splitting

For complex includes that generate large SQL queries:

**Example - DuplicateService.cs:**

```csharp
var group = await _dbContext.DuplicateGroups
    .AsNoTracking()
    .AsSplitQuery()  // Split into multiple queries
    .Include(g => g.Files)
    .FirstOrDefaultAsync(g => g.Id == id, ct);
```

### 4. Optimize Tracking

Use `AsNoTrackingWithIdentityResolution` when:
- Need to avoid duplicates in includes
- Still don't need change tracking

```csharp
var groups = await _dbContext.DuplicateGroups
    .AsNoTrackingWithIdentityResolution()
    .Include(g => g.Files)
    .ToListAsync(ct);
```

### 5. Add Missing Indexes

Review PhotosDbContext.cs and add indexes:

```csharp
// DuplicateGroup - status is frequently filtered
entity.HasIndex(e => e.Status);
entity.HasIndex(e => e.ValidatedAt);

// IndexedFile - file operations
entity.HasIndex(e => e.ThumbnailPath);
entity.HasIndex(e => new { e.DuplicateGroupId, e.IsDuplicate });
```

### 6. Batch Operations

Use `ExecuteUpdateAsync` and `ExecuteDeleteAsync` for bulk operations:

```csharp
// Instead of loading, modifying, and saving
await _dbContext.DuplicateGroups
    .Where(g => request.GroupIds.Contains(g.Id))
    .ExecuteUpdateAsync(
        setters => setters
            .SetProperty(g => g.Status, "validated")
            .SetProperty(g => g.ValidatedAt, DateTime.UtcNow),
        ct);
```

## Hot Path Candidates

Based on typical usage:

1. **GetByIdAsync** - `IndexedFileService`, `DuplicateService`
2. **GetGroupsAsync** - `DuplicateService` (list view)
3. **CheckNeedsReindexAsync** - `IndexedFileService` (batch operations)
4. **File existence checks** - Multiple services

## Files to Modify

```
src/Database/
└── PhotosDbContext.cs (add indexes)

src/Api/Services/
├── IndexedFileService.cs (compiled queries)
├── DuplicateService.cs (query splitting, compiled queries)
└── ScanDirectoryService.cs (compiled queries)

Database/Migrations/
└── YYYYMMDDHHmmss_AddPerformanceIndexes.cs (new)
```

## Performance Targets

After optimization:
- **Compiled queries**: 10-30% faster
- **Query splitting**: Reduce large query overhead
- **Proper indexes**: 50-90% faster filtered queries
- **Batch operations**: 10-100x faster bulk updates

## Measurement

Before and after:
```csharp
var sw = Stopwatch.StartNew();
// Query
sw.Stop();
_logger.LogInformation("Query took {Ms}ms", sw.ElapsedMilliseconds);
```

Or use BenchmarkDotNet:
```csharp
[Benchmark]
public async Task GetById_Compiled() { ... }

[Benchmark]
public async Task GetById_Regular() { ... }
```

## Benefits

- **Performance**: Faster query execution
- **Scalability**: Better handling of large datasets
- **Database Load**: Reduced query complexity
- **User Experience**: Faster page loads

## Risks

- **Complexity**: Compiled queries are less flexible
- **Over-optimization**: Premature optimization without profiling
- **Testing**: Need to verify behavior unchanged

## Related Tasks

- `05-performance/*` - General performance improvements
- `13-code-quality/001-static-analysis-configuration.md` - Could add EF analyzer

## References

- [EF Core Compiled Queries](https://docs.microsoft.com/en-us/ef/core/performance/advanced-performance-topics#compiled-queries)
- [Query Splitting](https://docs.microsoft.com/en-us/ef/core/querying/single-split-queries)
- [Performance Best Practices](https://docs.microsoft.com/en-us/ef/core/performance/)

## Completion Checklist

- [ ] Profile current query performance
- [ ] Identify top 10 hot path queries
- [ ] Implement compiled queries for hot paths
- [ ] Add query splitting where beneficial
- [ ] Review and optimize tracking strategies
- [ ] Create migration for missing indexes
- [ ] Run performance benchmarks (before/after)
- [ ] Document performance improvements
- [ ] All tests passing
- [ ] PR created and reviewed
