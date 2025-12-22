# 004: Bulk Override by Path Pattern

**Status**: 🔲 Not Started
**Priority**: P2
**Issue**: [#68](https://github.com/gbolabs/photos-index/issues/68)
**Branch**: `feature/bulk-override`
**Estimated Complexity**: Medium
**Target Release**: v0.3.0

## Objective

Allow users to bulk-override original selection based on path patterns, e.g., "For all duplicates in both /photos and /public, keep /photos".

## Dependencies

- `12-002` Batch Validation (status field)
- `12-003` Selection Algorithm (scoring infrastructure)

## Acceptance Criteria

- [ ] "Bulk Override" modal/panel in duplicates page
- [ ] Path pattern inputs: "Keep from" and "Remove from"
- [ ] Autocomplete with existing paths in duplicate groups
- [ ] Preview count of matching groups before applying
- [ ] Preview list of affected groups (first 10-20)
- [ ] Apply to pending only, or include validated
- [ ] Confirmation dialog with impact summary
- [ ] Activity log of bulk operations

## Technical Design

### UI Design

```
┌─────────────────────────────────────────────────────────────────┐
│ 🔄 Bulk Override by Path                                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│ When duplicates exist in BOTH paths:                           │
│                                                                 │
│   Keep files from:    [/photos/*_______________] ▾             │
│   Remove files from:  [/public/*_______________] ▾             │
│                                                                 │
│   Scope: ○ Pending only (28,150)                               │
│          ○ Include validated (30,247)                          │
│                                                                 │
│ ─────────────────────────────────────────────────────────────  │
│                                                                 │
│ Preview: 12,847 groups match this pattern                      │
│                                                                 │
│ Examples:                                                       │
│   🟢 /photos/2024/IMG_001.jpg  ←  /public/backup/IMG_001.jpg   │
│   🟢 /photos/trips/beach.png   ←  /public/camera/beach.png     │
│   🟢 /photos/family/kid.heic   ←  /public/iphone/kid.heic      │
│   ... and 12,844 more                                          │
│                                                                 │
│ [Cancel] [Preview All] [Apply to 12,847 Groups]                │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### API Endpoints

```
POST /api/duplicate-groups/bulk-override/preview
Body: {
  keepPathPattern: string,      // e.g., "/photos/*"
  removePathPattern: string,    // e.g., "/public/*"
  scope: 'pending' | 'all'
}
Response: {
  matchCount: number,
  examples: DuplicateGroupDto[]  // First 20
}

POST /api/duplicate-groups/bulk-override/apply
Body: {
  keepPathPattern: string,
  removePathPattern: string,
  scope: 'pending' | 'all'
}
Response: {
  applied: number,
  validated: number  // If auto-validating
}
```

### Pattern Matching

```csharp
public class BulkOverrideService
{
    public async Task<BulkOverridePreview> PreviewOverride(
        string keepPattern,
        string removePattern,
        string scope)
    {
        // Find groups where:
        // - At least one file matches keepPattern
        // - At least one file matches removePattern
        // - They're different files (not same file matching both)

        var query = _context.DuplicateGroups
            .Include(g => g.Files)
            .Where(g => scope == "all" || g.Status == "pending")
            .Where(g => g.Files.Any(f => MatchesPattern(f.FilePath, keepPattern))
                     && g.Files.Any(f => MatchesPattern(f.FilePath, removePattern)));

        return new BulkOverridePreview
        {
            MatchCount = await query.CountAsync(),
            Examples = await query.Take(20).ToListAsync()
        };
    }
}
```

## Files to Create/Modify

### Backend
- `src/Api/Controllers/DuplicateGroupsController.cs` - Add bulk override endpoints
- `src/Api/Services/BulkOverrideService.cs` (new)
- `src/Shared/Dtos/BulkOverrideRequest.cs` (new)
- `src/Shared/Dtos/BulkOverridePreview.cs` (new)

### Frontend
- `src/Web/src/app/features/duplicates/components/bulk-override-dialog/` (new)
- `src/Web/src/app/services/duplicate.service.ts` - Add bulk override methods

## Test Coverage

- Pattern matching unit tests
- Preview accuracy tests
- Apply operation tests
- Edge cases: overlapping patterns, no matches
